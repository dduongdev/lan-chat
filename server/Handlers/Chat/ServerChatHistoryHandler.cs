using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.Chat
{
    public class ServerChatHistoryHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;

        public string RoutingKey => RoutingKeys.ChatHistoryReq;

        public ServerChatHistoryHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received ChatHistoryReq from '{session.Username}'.");
            try
            {
                var request = payload.Deserialize<ChatHistoryRequestPayload>();
                if (request == null || string.IsNullOrWhiteSpace(request.TargetId))
                {
                    Console.WriteLine("[Server] ChatHistoryReq payload is invalid.");
                    return;
                }

                if (string.IsNullOrEmpty(session.Username))
                {
                    Console.WriteLine("[Server] ChatHistoryReq rejected: user not authenticated.");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Tìm User IDs
                var currentUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
                var targetUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.TargetId);

                if (currentUser == null || targetUser == null)
                {
                    Console.WriteLine("[Server] User not found for chat history.");
                    await session.SendAsync(RoutingKeys.ChatHistoryRes, new ChatHistoryResponsePayload
                    {
                        TargetId = request.TargetId,
                        Messages = new()
                    });
                    return;
                }

                // Truy vấn lịch sử: tin nhắn giữa 2 người (cả 2 chiều)
                var query = dbContext.Messages
                    .Where(m =>
                        (m.SenderId == currentUser.Id && m.ReceiverId == targetUser.Id) ||
                        (m.SenderId == targetUser.Id && m.ReceiverId == currentUser.Id));

                // Phân trang theo thời gian
                if (request.BeforeTimestamp.HasValue)
                {
                    query = query.Where(m => m.SentAt < request.BeforeTimestamp.Value);
                }

                var limit = request.Limit > 0 ? request.Limit : 20;

                var messages = await query
                    .OrderByDescending(m => m.SentAt)
                    .Take(limit)
                    .Include(m => m.Sender)
                    .Select(m => new ChatMessageDto
                    {
                        ServerMessageId = m.Id,
                        Sender = m.Sender.Username,
                        Content = m.Content,
                        SentAt = m.SentAt
                    })
                    .ToListAsync();

                // Đảo ngược để trả về thứ tự cũ -> mới
                messages.Reverse();

                Console.WriteLine($"[Server] Returning {messages.Count} history messages for '{request.TargetId}'.");
                await session.SendAsync(RoutingKeys.ChatHistoryRes, new ChatHistoryResponsePayload
                {
                    TargetId = request.TargetId,
                    Messages = messages
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error processing ChatHistoryReq: {ex.Message}");
            }
        }
    }
}
