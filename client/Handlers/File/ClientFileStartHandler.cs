using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Client.Transfers;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.File
{
    public class ClientFileStartHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.FileStartTransfer;

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var start = payload.Deserialize<FileStartPayload>();
            if (start == null) return;

            ChatService.Instance.RaiseFileStartReceived(start.FileTransferId, start.TransferHost, start.TransferPort);

            string? role = session.GetMetadata($"file_role_{start.FileTransferId}");

            if (role == "RECEIVER")
            {
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
                        ChatService.Instance.RaiseFileStatusReceived(start.FileTransferId, "Completed");
                        await session.SendAsync(RoutingKeys.FileStatusUpdate, new FileStatusPayload
                        {
                            FileTransferId = start.FileTransferId,
                            Status = "Completed"
                        });
                    }
                    catch (Exception ex)
                    {
                        ChatService.Instance.RaiseFileStatusReceived(start.FileTransferId, $"Failed: {ex.Message}");
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
                string? filePath = session.GetMetadata($"file_path_{start.FileTransferId}");
                if (string.IsNullOrEmpty(filePath))
                    filePath = session.GetMetadata("pending_file_path");

                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                    return;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await FileTransferClient.StartSendingAsync(start.TransferHost, start.TransferPort, filePath);
                        ChatService.Instance.RaiseFileStatusReceived(start.FileTransferId, "Completed");
                        await session.SendAsync(RoutingKeys.FileStatusUpdate, new FileStatusPayload
                        {
                            FileTransferId = start.FileTransferId,
                            Status = "Completed"
                        });
                    }
                    catch (Exception ex)
                    {
                        ChatService.Instance.RaiseFileStatusReceived(start.FileTransferId, $"Failed: {ex.Message}");
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
