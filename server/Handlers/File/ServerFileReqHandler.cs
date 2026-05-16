using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.Entities;
using LanChat.Server.State;
using LanChat.Server.Transfers;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.File
{
    /// <summary>
    /// UC-06 v2: Xử lý yêu cầu Upload và Download trên Port 8080 (Control Plane).
    /// - FileUploadReq: Tạo bản ghi DB, sinh Token, trả về TransferTokenResponsePayload.
    /// - FileDownloadReq: Kiểm tra file, sinh Token, trả về TransferTokenResponsePayload.
    /// </summary>
    public class ServerFileUploadHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;
        private readonly FileStreamManager _fileStreamManager;

        public string RoutingKey => RoutingKeys.FileUploadReq;

        public ServerFileUploadHandler(IServiceProvider serviceProvider, SessionManager sessionManager, FileStreamManager fileStreamManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
            _fileStreamManager = fileStreamManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received FileUploadReq from '{session.Username}'.");
            try
            {
                var request = payload.Deserialize<FileUploadRequestPayload>();
                if (request == null || string.IsNullOrWhiteSpace(request.FileName))
                {
                    Console.WriteLine("[Server] FileUploadReq payload is invalid.");
                    return;
                }

                if (string.IsNullOrEmpty(session.Username))
                {
                    Console.WriteLine("[Server] FileUploadReq rejected: user not authenticated.");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Tìm Uploader trong DB
                var uploader = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
                if (uploader == null)
                {
                    Console.WriteLine($"[Server] Uploader '{session.Username}' not found in DB.");
                    return;
                }

                // Tạo bản ghi FileTransfer trong DB với Status = "Uploading"
                var fileId = Guid.NewGuid();
                var storagePath = $"{fileId}.dat";
                var entity = new FileTransfer
                {
                    Id = fileId,
                    UploaderId = uploader.Id,
                    FileName = request.FileName,
                    FileSize = request.FileSize,
                    FileHash = request.FileHash,
                    StoragePath = storagePath,
                    Status = "Uploading",
                    CreatedAt = DateTime.UtcNow,
                    TargetType = request.TargetType,
                    TargetId = request.TargetId
                };

                dbContext.FileTransfers.Add(entity);
                await dbContext.SaveChangesAsync();

                Console.WriteLine($"[Server] FileTransfer record created. ID={fileId}, File={request.FileName}, Size={request.FileSize}, Hash={request.FileHash}");

                // Sinh TransferToken và lưu vào MemoryCache (FileStreamManager)
                var token = Guid.NewGuid();
                _fileStreamManager.RegisterToken(token, new TransferContext
                {
                    Action = "UPLOAD",
                    FileId = fileId,
                    ExpectedHash = request.FileHash,
                    TargetType = request.TargetType,
                    TargetId = request.TargetId,
                    UploaderUsername = session.Username
                });

                // Trả về TransferTokenResponsePayload (kèm ClientRequestId để Client xác định file)
                var response = new TransferTokenResponsePayload
                {
                    Success = true,
                    Action = "UPLOAD",
                    TransferToken = token,
                    FileId = fileId,
                    ClientRequestId = request.ClientRequestId
                };
                await session.SendAsync(RoutingKeys.FileTransferRes, response);
                Console.WriteLine($"[Server] Upload token issued for file {fileId}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error processing FileUploadReq: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Xử lý yêu cầu Download file từ Server.
    /// </summary>
    public class ServerFileDownloadHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;
        private readonly FileStreamManager _fileStreamManager;

        public string RoutingKey => RoutingKeys.FileDownloadReq;

        public ServerFileDownloadHandler(IServiceProvider serviceProvider, SessionManager sessionManager, FileStreamManager fileStreamManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
            _fileStreamManager = fileStreamManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received FileDownloadReq from '{session.Username}'.");
            try
            {
                var request = payload.Deserialize<FileDownloadRequestPayload>();
                if (request == null)
                {
                    Console.WriteLine("[Server] FileDownloadReq payload is invalid.");
                    return;
                }

                if (string.IsNullOrEmpty(session.Username))
                {
                    Console.WriteLine("[Server] FileDownloadReq rejected: user not authenticated.");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Kiểm tra file tồn tại và Available
                var transfer = await dbContext.FileTransfers
                    .FirstOrDefaultAsync(ft => ft.Id == request.FileId && ft.Status == "Available");

                if (transfer == null)
                {
                    Console.WriteLine($"[Server] FileTransfer {request.FileId} not found or not available.");
                    await session.SendAsync(RoutingKeys.FileTransferRes, new TransferTokenResponsePayload
                    {
                        Success = false,
                        Action = "DOWNLOAD",
                        FileId = request.FileId,
                        ErrorMessage = "File not found or not available."
                    });
                    return;
                }

                // (Bảo mật) Kiểm tra quyền truy cập
                var requester = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
                if (requester == null) return;

                bool isAuthorized = false;

                // 1. Người tải lên luôn có quyền tải xuống
                if (transfer.UploaderId == requester.Id)
                {
                    isAuthorized = true;
                }
                // 2. Nếu là file PRIVATE -> Người nhận có quyền tải xuống
                else if (transfer.TargetType == "PRIVATE" && transfer.TargetId == session.Username)
                {
                    isAuthorized = true;
                }
                // 3. Nếu là file GROUP -> Thành viên nhóm có quyền tải xuống
                else if (transfer.TargetType == "GROUP" && Guid.TryParse(transfer.TargetId, out var groupId))
                {
                    var isMember = await dbContext.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == requester.Id);
                    if (isMember)
                    {
                        isAuthorized = true;
                    }
                }

                if (!isAuthorized)
                {
                    Console.WriteLine($"[Server] FileDownloadReq rejected: user '{session.Username}' is not authorized to download file {request.FileId}.");
                    await session.SendAsync(RoutingKeys.FileTransferRes, new TransferTokenResponsePayload
                    {
                        Success = false,
                        Action = "DOWNLOAD",
                        FileId = request.FileId,
                        ErrorMessage = "Unauthorized to download this file."
                    });
                    return;
                }
                // Sinh TransferToken
                var token = Guid.NewGuid();
                _fileStreamManager.RegisterToken(token, new TransferContext
                {
                    Action = "DOWNLOAD",
                    FileId = transfer.Id
                });

                var response = new TransferTokenResponsePayload
                {
                    Success = true,
                    Action = "DOWNLOAD",
                    TransferToken = token,
                    FileId = transfer.Id
                };
                await session.SendAsync(RoutingKeys.FileTransferRes, response);
                Console.WriteLine($"[Server] Download token issued for file {transfer.Id}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error processing FileDownloadReq: {ex.Message}");
            }
        }
    }
}
