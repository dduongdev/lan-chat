using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Transfers;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.File
{
    /// <summary>
    /// Xử lý thông báo bắt đầu truyền file từ Server.
    /// Xác định vai trò (Sender/Receiver) và khởi động truyền/nhận file.
    /// </summary>
    public class ClientFileStartHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.FileStartTransfer;

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var start = payload.Deserialize<FileStartPayload>();
            if (start == null) return;

            Console.WriteLine($"[Client] FileStart received: Transfer={start.FileTransferId}, Host={start.TransferHost}, Port={start.TransferPort}");

            // Xác định vai trò dựa trên metadata đã lưu
            string? role = session.GetMetadata($"file_role_{start.FileTransferId}");

            if (role == "RECEIVER")
            {
                // Nhận file
                string? fileName = session.GetMetadata($"file_name_{start.FileTransferId}");
                string? fileSizeStr = session.GetMetadata($"file_size_{start.FileTransferId}");
                long fileSize = long.TryParse(fileSizeStr, out var fs) ? fs : 0;

                string savePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "LanChat_Received",
                    fileName ?? $"received_{start.FileTransferId}"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await FileTransferClient.StartReceivingAsync(start.TransferHost, start.TransferPort, savePath, fileSize);

                        // Báo Server hoàn tất
                        await session.SendAsync(RoutingKeys.FileStatusUpdate, new FileStatusPayload
                        {
                            FileTransferId = start.FileTransferId,
                            Status = "Completed"
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FileTransfer] Receive failed: {ex.Message}");
                        await session.SendAsync(RoutingKeys.FileStatusUpdate, new FileStatusPayload
                        {
                            FileTransferId = start.FileTransferId,
                            Status = "Failed"
                        });
                    }
                });
            }
            else
            {
                // Mình là Sender
                string? filePath = session.GetMetadata($"file_path_{start.FileTransferId}");
                // Fallback: lấy từ pending_file_path (do Client set trước khi gửi FileRequest)
                if (string.IsNullOrEmpty(filePath))
                {
                    filePath = session.GetMetadata("pending_file_path");
                }
                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                {
                    Console.WriteLine($"[FileTransfer] Source file not found: {filePath}");
                    return;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await FileTransferClient.StartSendingAsync(start.TransferHost, start.TransferPort, filePath);

                        // Báo Server hoàn tất
                        await session.SendAsync(RoutingKeys.FileStatusUpdate, new FileStatusPayload
                        {
                            FileTransferId = start.FileTransferId,
                            Status = "Completed"
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FileTransfer] Send failed: {ex.Message}");
                        await session.SendAsync(RoutingKeys.FileStatusUpdate, new FileStatusPayload
                        {
                            FileTransferId = start.FileTransferId,
                            Status = "Failed"
                        });
                    }
                });
            }
        }
    }
}
