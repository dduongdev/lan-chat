using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.State;
using LanChat.Server.Transfers;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.File
{
    /// <summary>
    /// Xử lý phản hồi từ Receiver (chấp nhận hoặc từ chối file).
    /// Nếu accepted: cập nhật DB, mở kênh TCP, gửi FileStartPayload cho cả 2 bên.
    /// Nếu rejected: cập nhật DB, thông báo cho Sender.
    /// </summary>
    public class ServerFileResponseHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;
        private readonly FileTransferManager _fileTransferManager;

        public string RoutingKey => RoutingKeys.FileResponse;

        public ServerFileResponseHandler(IServiceProvider serviceProvider, SessionManager sessionManager, FileTransferManager fileTransferManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
            _fileTransferManager = fileTransferManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received FileResponse from '{session.Username}'.");
            try
            {
                var response = payload.Deserialize<FileResponsePayload>();
                if (response == null)
                {
                    Console.WriteLine("[Server] FileResponse payload is invalid.");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Tìm bản ghi FileTransfer
                var transfer = await dbContext.FileTransfers
                    .Include(ft => ft.Sender)
                    .Include(ft => ft.Receiver)
                    .FirstOrDefaultAsync(ft => ft.Id == response.FileTransferId);

                if (transfer == null)
                {
                    Console.WriteLine($"[Server] FileTransfer {response.FileTransferId} not found.");
                    return;
                }

                if (!response.Accepted)
                {
                    // Từ chối: cập nhật DB và thông báo Sender
                    transfer.Status = "Rejected";
                    await dbContext.SaveChangesAsync();
                    Console.WriteLine($"[Server] FileTransfer {response.FileTransferId} was REJECTED by '{session.Username}'.");

                    // Thông báo Sender
                    if (_sessionManager.TryGet(transfer.Sender.Username, out var senderSession) && senderSession != null)
                    {
                        var statusPayload = new FileStatusPayload
                        {
                            FileTransferId = transfer.Id,
                            Status = "Rejected"
                        };
                        await senderSession.SendAsync(RoutingKeys.FileStatusUpdate, statusPayload);
                    }
                    return;
                }

                // Chấp nhận: cập nhật DB
                transfer.Status = "Transferring";
                await dbContext.SaveChangesAsync();
                Console.WriteLine($"[Server] FileTransfer {response.FileTransferId} ACCEPTED. Opening transfer channel...");

                // Mở kênh TCP tạm thời
                int port = _fileTransferManager.InitiateTransfer(transfer.Id, transfer.FileSize);

                // Gửi FileStartPayload cho cả Sender và Receiver
                var startPayload = new FileStartPayload
                {
                    FileTransferId = transfer.Id,
                    TransferHost = "127.0.0.1", // Localhost cho LAN testing
                    TransferPort = port
                };

                // Gửi cho Sender
                if (_sessionManager.TryGet(transfer.Sender.Username, out var senderSess) && senderSess != null)
                {
                    await senderSess.SendAsync(RoutingKeys.FileStartTransfer, startPayload);
                    Console.WriteLine($"[Server] FileStart sent to Sender '{transfer.Sender.Username}'.");
                }

                // Gửi cho Receiver
                if (_sessionManager.TryGet(transfer.Receiver.Username, out var receiverSess) && receiverSess != null)
                {
                    await receiverSess.SendAsync(RoutingKeys.FileStartTransfer, startPayload);
                    Console.WriteLine($"[Server] FileStart sent to Receiver '{transfer.Receiver.Username}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error processing FileResponse: {ex.Message}");
            }
        }
    }
}
