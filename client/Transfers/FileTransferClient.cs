using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace LanChat.Client.Transfers
{
    /// <summary>
    /// UC-06 v2: Client-side file transfer logic.
    /// - Upload: Kết nối Port 8081, gửi Token + Stream dữ liệu.
    /// - Download: Kết nối Port 8081, gửi Token, nhận Stream dữ liệu.
    /// </summary>
    public static class FileTransferClient
    {
        private const int BufferSize = 81920; // 80KB chunks

        /// <summary>
        /// Tính SHA-256 hash của file.
        /// </summary>
        public static async Task<string> ComputeHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            var hashBytes = await sha256.ComputeHashAsync(fileStream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Upload file lên Server qua Port 8081.
        /// 1. Kết nối TCP đến ServerIP:8081
        /// 2. Ghi 16 bytes Token lên đầu luồng
        /// 3. Sao chép FileStream vào NetworkStream (kèm mã hoá AES)
        /// </summary>
        public static async Task UploadAsync(string host, int port, Guid transferToken, string filePath, byte[]? aesKey = null, byte[]? aesIV = null, Action<long, long>? onProgress = null)
        {
            Console.WriteLine($"[FileTransfer] Connecting to data plane {host}:{port} for UPLOAD...");
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port);
            Console.WriteLine($"[FileTransfer] Connected. Uploading file: {filePath}");

            using var networkStream = tcpClient.GetStream();

            // Ghi 16 bytes Token lên đầu luồng
            var tokenBytes = transferToken.ToByteArray();
            await networkStream.WriteAsync(tokenBytes, 0, tokenBytes.Length);
            Console.WriteLine($"[FileTransfer] Token sent: {transferToken}");

            // Sao chép file vào network stream
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            long totalSent = 0;
            long fileSize = fileStream.Length;

            if (aesKey != null && aesIV != null)
            {
                using var aes = Aes.Create();
                aes.Key = aesKey;
                aes.IV = aesIV;
                // leaveOpen: true để tránh dispose networkStream sớm do CryptoStream.Dispose
                using var cryptoStream = new CryptoStream(networkStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
                
                var buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await cryptoStream.WriteAsync(buffer, 0, bytesRead);
                    totalSent += bytesRead;
                    onProgress?.Invoke(totalSent, fileSize);
                }
                
                // Cần đảm bảo block cuối cùng được flush và viết vào network
                await cryptoStream.FlushFinalBlockAsync();
            }
            else
            {
                var buffer = new byte[BufferSize];
                int bytesRead;
                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await networkStream.WriteAsync(buffer, 0, bytesRead);
                    totalSent += bytesRead;
                    onProgress?.Invoke(totalSent, fileSize);
                }
            }

            await networkStream.FlushAsync();
            // Đóng phía gửi để Server biết đã hết dữ liệu
            tcpClient.Client.Shutdown(System.Net.Sockets.SocketShutdown.Send);
            
            // Chờ Server xử lý xong và đóng luồng từ phía Server (tránh đóng socket quá sớm gây lỗi RST)
            var dummy = new byte[1];
            await networkStream.ReadAsync(dummy, 0, 1);

            Console.WriteLine($"[FileTransfer] Upload completed. Total: {totalSent} bytes.");
        }

        /// <summary>
        /// Download file từ Server qua Port 8081.
        /// 1. Kết nối TCP đến ServerIP:8081
        /// 2. Ghi 16 bytes Token lên đầu luồng
        /// 3. Sao chép NetworkStream vào FileStream (kèm giải mã AES)
        /// 4. Kiểm tra SHA-256
        /// </summary>
        public static async Task<bool> DownloadAsync(string host, int port, Guid transferToken, string savePath, string expectedHash, byte[]? aesKey = null, byte[]? aesIV = null, Action<long, long>? onProgress = null)
        {
            Console.WriteLine($"[FileTransfer] Connecting to data plane {host}:{port} for DOWNLOAD...");
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port);
            Console.WriteLine($"[FileTransfer] Connected. Downloading to: {savePath}");

            using var networkStream = tcpClient.GetStream();

            // Ghi 16 bytes Token lên đầu luồng
            var tokenBytes = transferToken.ToByteArray();
            await networkStream.WriteAsync(tokenBytes, 0, tokenBytes.Length);
            Console.WriteLine($"[FileTransfer] Token sent: {transferToken}");

            // Đảm bảo thư mục đích tồn tại
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

            // Nhận dữ liệu từ network stream
            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
            {
                long totalReceived = 0;
                
                if (aesKey != null && aesIV != null)
                {
                    using var aes = Aes.Create();
                    aes.Key = aesKey;
                    aes.IV = aesIV;
                    using var cryptoStream = new CryptoStream(networkStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
                    
                    var buffer = new byte[BufferSize];
                    int bytesRead;
                    while ((bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalReceived += bytesRead;
                        onProgress?.Invoke(totalReceived, 0);
                    }
                }
                else
                {
                    var buffer = new byte[BufferSize];
                    int bytesRead;
                    while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalReceived += bytesRead;
                        onProgress?.Invoke(totalReceived, 0);
                    }
                }

                await fileStream.FlushAsync();
                Console.WriteLine($"[FileTransfer] Download stream received. Total: {totalReceived} bytes.");
            }

            // Kiểm tra SHA-256
            if (!string.IsNullOrEmpty(expectedHash))
            {
                string computedHash = await ComputeHashAsync(savePath);
                Console.WriteLine($"[FileTransfer] Hash verification: expected={expectedHash}, computed={computedHash}");

                if (computedHash != expectedHash)
                {
                    Console.WriteLine("[FileTransfer] Hash mismatch! Deleting corrupted file.");
                    File.Delete(savePath);
                    return false;
                }
                Console.WriteLine("[FileTransfer] Hash verification OK.");
            }

            Console.WriteLine($"[FileTransfer] Download completed successfully. Saved to: {savePath}");
            return true;
        }
    }
}
