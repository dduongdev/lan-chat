using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.Entities;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.File
{
    /// <summary>
    /// Xử lý yêu cầu gửi file từ Sender.
    /// 1. Tạo bản ghi FileTransfer trong DB (Status: Pending)
    /// 2. Gửi FileOfferPayload đến Receiver
    /// </summary>
    public class ServerFileRequestHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;

        public string RoutingKey => RoutingKeys.FileRequest;

        public ServerFileRequestHandler(IServiceProvider serviceProvider, SessionManager sessionManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received FileRequest from '{session.Username}'.");
            try
            {
                var request = payload.Deserialize<FileRequestPayload>();
                if (request == null || string.IsNullOrWhiteSpace(request.FileName))
                {
                    Console.WriteLine("[Server] FileRequest payload is invalid.");
                    return;
                }

                if (string.IsNullOrEmpty(session.Username))
                {
                    Console.WriteLine("[Server] FileRequest rejected: user not authenticated.");
                    return;
                }

                // Kiểm tra Receiver online
                if (!_sessionManager.TryGet(request.ReceiverUsername, out var receiverSession) || receiverSession == null)
                {
                    Console.WriteLine($"[Server] Receiver '{request.ReceiverUsername}' is offline. Cannot initiate file transfer.");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Tìm User IDs
                var sender = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
                var receiver = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.ReceiverUsername);

                if (sender == null || receiver == null)
                {
                    Console.WriteLine("[Server] Sender or Receiver not found in DB.");
                    return;
                }

                // Tạo bản ghi FileTransfer trong DB
                var transferId = Guid.NewGuid();
                var entity = new FileTransfer
                {
                    Id = transferId,
                    SenderId = sender.Id,
                    ReceiverId = receiver.Id,
                    FileName = request.FileName,
                    FileSize = request.FileSize,
                    Status = "Pending",
                    RequestedAt = DateTime.UtcNow
                };

                dbContext.FileTransfers.Add(entity);
                await dbContext.SaveChangesAsync();

                Console.WriteLine($"[Server] FileTransfer record created. ID={transferId}, File={request.FileName}, Size={request.FileSize}");

                // Gửi FileOffer đến Receiver
                var offer = new FileOfferPayload
                {
                    FileTransferId = transferId,
                    SenderUsername = session.Username,
                    FileName = request.FileName,
                    FileSize = request.FileSize
                };
                await receiverSession.SendAsync(RoutingKeys.FileOffer, offer);
                Console.WriteLine($"[Server] FileOffer sent to '{request.ReceiverUsername}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error processing FileRequest: {ex.Message}");
            }
        }
    }
}
