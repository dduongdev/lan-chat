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
    /// <summary>
    /// UC-06 v2: Xử lý phản hồi TransferToken từ Server.
    /// - Nếu Action == "UPLOAD": kết nối Port 8081, gửi Token + stream file.
    /// - Nếu Action == "DOWNLOAD": kết nối Port 8081, gửi Token, nhận stream.
    /// </summary>
    public class ClientFileTransferResHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.FileTransferRes;

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var response = payload.Deserialize<TransferTokenResponsePayload>();
            if (response == null) return;

            Console.WriteLine($"[Client] Received FileTransferRes: Success={response.Success}, Action={response.Action}, FileId={response.FileId}");

            if (!response.Success)
            {
                Console.WriteLine($"[Client] File transfer rejected: {response.ErrorMessage}");
                ChatService.Instance.RaiseFileTransferError(response.FileId, response.ErrorMessage ?? "Request rejected.");
                return;
            }

            if (response.Action == "UPLOAD")
            {
                await HandleUploadAsync(session, response);
            }
            else if (response.Action == "DOWNLOAD")
            {
                await HandleDownloadAsync(session, response);
            }
        }

        private async Task HandleUploadAsync(SessionHandler session, TransferTokenResponsePayload response)
        {
            // Lấy file path bằng ClientRequestId do Client sinh ra trước đó
            string metadataKey = $"pending_upload_path_{response.ClientRequestId}";
            string? filePath = session.GetMetadata(metadataKey);

            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                Console.WriteLine($"[Client] Upload failed: file path not found for ClientRequestId={response.ClientRequestId}.");
                return;
            }

            // Dọn dẹp metadata sau khi đã lấy được file path
            session.RemoveMetadata(metadataKey);

            // Lấy server host từ metadata hoặc mặc định
            string host = session.GetMetadata("server_host") ?? "127.0.0.1";

            Console.WriteLine($"[Client] Starting upload for FileId={response.FileId}, Token={response.TransferToken}");
            ChatService.Instance.RaiseFileUploadStarted(response.FileId);

            _ = Task.Run(async () =>
            {
                try
                {
                    await FileTransferClient.UploadAsync(
                        host, 8081, response.TransferToken, filePath,
                        session.Cipher?.Key, session.Cipher?.IV,
                        (sent, total) => ChatService.Instance.RaiseFileTransferProgress(response.FileId, sent, total)
                    );
                    Console.WriteLine($"[Client] Upload completed for FileId={response.FileId}. Waiting for server confirmation...");
                    // Server sẽ gửi chat.recv sau khi verify hash thành công
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Client] Upload failed for FileId={response.FileId}: {ex.Message}");
                    ChatService.Instance.RaiseFileTransferError(response.FileId, ex.Message);
                }
            });
        }

        private async Task HandleDownloadAsync(SessionHandler session, TransferTokenResponsePayload response)
        {
            // Lấy thông tin download từ metadata
            string? savePath = session.GetMetadata($"download_save_path_{response.FileId}");
            string? expectedHash = session.GetMetadata($"download_hash_{response.FileId}");

            if (string.IsNullOrEmpty(savePath))
            {
                // Sử dụng đường dẫn mặc định
                string? fileName = session.GetMetadata($"download_file_name_{response.FileId}");
                savePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "LanChat_Received",
                    fileName ?? $"download_{response.FileId}"
                );
            }

            string host = session.GetMetadata("server_host") ?? "127.0.0.1";

            Console.WriteLine($"[Client] Starting download for FileId={response.FileId}, Token={response.TransferToken}");
            ChatService.Instance.RaiseFileDownloadStarted(response.FileId);

            _ = Task.Run(async () =>
            {
                try
                {
                    bool success = await FileTransferClient.DownloadAsync(
                        host, 8081, response.TransferToken, savePath, expectedHash ?? "",
                        session.Cipher?.Key, session.Cipher?.IV,
                        (received, total) => ChatService.Instance.RaiseFileTransferProgress(response.FileId, received, total)
                    );

                    if (success)
                    {
                        Console.WriteLine($"[Client] Download completed for FileId={response.FileId}. Saved to {savePath}");
                        ChatService.Instance.RaiseFileDownloadCompleted(response.FileId, savePath);
                    }
                    else
                    {
                        Console.WriteLine($"[Client] Download failed for FileId={response.FileId}: hash mismatch.");
                        ChatService.Instance.RaiseFileTransferError(response.FileId, "Hash verification failed. File corrupted.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Client] Download failed for FileId={response.FileId}: {ex.Message}");
                    ChatService.Instance.RaiseFileTransferError(response.FileId, ex.Message);
                }
            });
        }
    }
}
