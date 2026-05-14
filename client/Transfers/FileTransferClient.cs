using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LanChat.Client.Transfers
{
    /// <summary>
    /// Đóng gói logic truyền/nhận file qua kênh TCP Stream riêng biệt.
    /// Kênh này hoàn toàn tách biệt với kênh Messaging chính.
    /// </summary>
    public static class FileTransferClient
    {
        private const int BufferSize = 8192; // 8KB chunks

        /// <summary>
        /// Gửi file lên Server proxy qua kênh TCP tạm thời.
        /// </summary>
        public static async Task StartSendingAsync(string host, int port, string filePath)
        {
            Console.WriteLine($"[FileTransfer] Connecting to transfer channel {host}:{port} as SENDER...");
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port);
            Console.WriteLine($"[FileTransfer] Connected. Sending file: {filePath}");

            using var networkStream = tcpClient.GetStream();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            var buffer = new byte[BufferSize];
            long totalSent = 0;
            long fileSize = fileStream.Length;

            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await networkStream.WriteAsync(buffer, 0, bytesRead);
                totalSent += bytesRead;

                // Báo cáo tiến độ mỗi 10%
                int progress = (int)(totalSent * 100 / fileSize);
                if (totalSent == fileSize || progress % 10 == 0)
                {
                    Console.WriteLine($"[FileTransfer] Sending... {totalSent}/{fileSize} bytes ({progress}%)");
                }
            }

            await networkStream.FlushAsync();
            Console.WriteLine($"[FileTransfer] File sent successfully. Total: {totalSent} bytes.");
        }

        /// <summary>
        /// Nhận file từ Server proxy qua kênh TCP tạm thời.
        /// </summary>
        public static async Task StartReceivingAsync(string host, int port, string savePath, long expectedSize)
        {
            Console.WriteLine($"[FileTransfer] Connecting to transfer channel {host}:{port} as RECEIVER...");
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port);
            Console.WriteLine($"[FileTransfer] Connected. Receiving file to: {savePath}");

            using var networkStream = tcpClient.GetStream();
            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write);

            var buffer = new byte[BufferSize];
            long totalReceived = 0;

            int bytesRead;
            while (totalReceived < expectedSize && (bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalReceived += bytesRead;

                int progress = (int)(totalReceived * 100 / expectedSize);
                if (totalReceived == expectedSize || progress % 10 == 0)
                {
                    Console.WriteLine($"[FileTransfer] Receiving... {totalReceived}/{expectedSize} bytes ({progress}%)");
                }
            }

            await fileStream.FlushAsync();
            Console.WriteLine($"[FileTransfer] File received successfully. Total: {totalReceived} bytes. Saved to: {savePath}");
        }
    }
}
