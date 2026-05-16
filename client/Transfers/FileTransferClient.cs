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
        /// 3. Sao chép FileStream vào NetworkStream
        /// </summary>
        public static async Task UploadAsync(string host, int port, Guid transferToken, string filePath, Action<long, long>? onProgress = null)
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
            var buffer = new byte[BufferSize];
            long totalSent = 0;
            long fileSize = fileStream.Length;

            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await networkStream.WriteAsync(buffer, 0, bytesRead);
                totalSent += bytesRead;

                onProgress?.Invoke(totalSent, fileSize);

                int progress = (int)(totalSent * 100 / fileSize);
                if (totalSent == fileSize || progress % 10 == 0)
                {
                    Console.WriteLine($"[FileTransfer] Uploading... {totalSent}/{fileSize} bytes ({progress}%)");
                }
            }

            await networkStream.FlushAsync();
            // Đóng phía gửi để Server biết đã hết dữ liệu
            tcpClient.Client.Shutdown(System.Net.Sockets.SocketShutdown.Send);
            Console.WriteLine($"[FileTransfer] Upload completed. Total: {totalSent} bytes.");
        }

        /// <summary>
        /// Download file từ Server qua Port 8081.
        /// 1. Kết nối TCP đến ServerIP:8081
        /// 2. Ghi 16 bytes Token lên đầu luồng
        /// 3. Sao chép NetworkStream vào FileStream
        /// 4. Kiểm tra SHA-256
        /// </summary>
        public static async Task<bool> DownloadAsync(string host, int port, Guid transferToken, string savePath, string expectedHash, Action<long, long>? onProgress = null)
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
                var buffer = new byte[BufferSize];
                long totalReceived = 0;
                int bytesRead;

                while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalReceived += bytesRead;

                    onProgress?.Invoke(totalReceived, 0);

                    if (totalReceived % (BufferSize * 10) == 0 || bytesRead == 0)
                    {
                        Console.WriteLine($"[FileTransfer] Downloading... {totalReceived} bytes received.");
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
