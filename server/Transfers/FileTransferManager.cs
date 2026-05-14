using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LanChat.Server.Transfers
{
    /// <summary>
    /// Quản lý các kênh TCP/IP tạm thời cho việc truyền file.
    /// Mỗi phiên truyền file sẽ có một TcpListener riêng trên port ngẫu nhiên.
    /// Server đóng vai trò Proxy: nhận stream từ Sender và chuyển tiếp sang Receiver.
    /// </summary>
    public sealed class FileTransferManager
    {
        private readonly ConcurrentDictionary<Guid, TransferSession> _activeSessions = new();

        /// <summary>
        /// Khởi tạo một kênh truyền file mới.
        /// Mở TcpListener trên port ngẫu nhiên, chờ 2 kết nối (Sender + Receiver), rồi proxy stream.
        /// </summary>
        /// <returns>Port đã mở.</returns>
        public int InitiateTransfer(Guid fileTransferId, long fileSize)
        {
            // Mở TcpListener trên port 0 (OS tự chọn port trống)
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var session = new TransferSession
            {
                FileTransferId = fileTransferId,
                Listener = listener,
                FileSize = fileSize
            };

            _activeSessions[fileTransferId] = session;

            Console.WriteLine($"[FileTransferManager] Opened transfer channel on port {port} for FileTransfer {fileTransferId}.");

            // Chạy task nền để accept 2 kết nối và proxy
            _ = Task.Run(async () => await RunProxyAsync(session));

            return port;
        }

        private async Task RunProxyAsync(TransferSession session)
        {
            TcpClient? senderClient = null;
            TcpClient? receiverClient = null;

            try
            {
                // Timeout 30 giây cho mỗi kết nối
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                // Accept kết nối thứ 1 (Sender kết nối trước)
                Console.WriteLine($"[FileTransferManager] Waiting for Sender to connect...");
                senderClient = await session.Listener.AcceptTcpClientAsync(cts.Token);
                Console.WriteLine($"[FileTransferManager] Sender connected from {senderClient.Client.RemoteEndPoint}.");

                // Accept kết nối thứ 2 (Receiver)
                Console.WriteLine($"[FileTransferManager] Waiting for Receiver to connect...");
                receiverClient = await session.Listener.AcceptTcpClientAsync(cts.Token);
                Console.WriteLine($"[FileTransferManager] Receiver connected from {receiverClient.Client.RemoteEndPoint}.");

                // Proxy: đọc từ Sender stream, ghi vào Receiver stream
                using var senderStream = senderClient.GetStream();
                using var receiverStream = receiverClient.GetStream();

                byte[] buffer = new byte[8192]; // 8KB chunks
                long totalBytesRead = 0;

                while (totalBytesRead < session.FileSize)
                {
                    int bytesRead = await senderStream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    if (bytesRead == 0) break; // Sender đóng stream

                    await receiverStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                    totalBytesRead += bytesRead;
                }

                await receiverStream.FlushAsync(cts.Token);
                Console.WriteLine($"[FileTransferManager] File transfer {session.FileTransferId} completed. {totalBytesRead} bytes transferred.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[FileTransferManager] Transfer {session.FileTransferId} timed out.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileTransferManager] Transfer {session.FileTransferId} error: {ex.Message}");
            }
            finally
            {
                // Dọn dẹp
                senderClient?.Close();
                receiverClient?.Close();
                session.Listener.Stop();
                _activeSessions.TryRemove(session.FileTransferId, out _);
                Console.WriteLine($"[FileTransferManager] Transfer channel {session.FileTransferId} closed.");
            }
        }

        private class TransferSession
        {
            public Guid FileTransferId { get; set; }
            public TcpListener Listener { get; set; } = null!;
            public long FileSize { get; set; }
        }
    }
}
