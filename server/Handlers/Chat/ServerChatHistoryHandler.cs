using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.Entities;
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

                var currentUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
                if (currentUser == null) return;

                IQueryable<Message> query = dbContext.Messages;

                if (request.TargetType == "PRIVATE")
                {
                    var targetUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.TargetId);
                    if (targetUser == null)
                    {
                        await session.SendAsync(RoutingKeys.ChatHistoryRes, new ChatHistoryResponsePayload { TargetId = request.TargetId, Messages = new() });
                        return;
                    }
                    query = query.Where(m =>
                        (m.SenderId == currentUser.Id && m.ReceiverId == targetUser.Id) ||
                        (m.SenderId == targetUser.Id && m.ReceiverId == currentUser.Id));
                }
                else if (request.TargetType == "GROUP")
                {
                    if (Guid.TryParse(request.TargetId, out var groupId))
                    {
                        query = query.Where(m => m.GroupId == groupId);
                    }
                    else
                    {
                        await session.SendAsync(RoutingKeys.ChatHistoryRes, new ChatHistoryResponsePayload { TargetId = request.TargetId, Messages = new() });
                        return;
                    }
                }
                else if (request.TargetType == "ALL" || request.TargetType == "BROADCAST")
                {
                    query = query.Where(m => m.ReceiverId == null && m.GroupId == null);
                }

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
                        SentAt = m.SentAt,
                        MessageType = m.MessageType,
                        FileId = m.FileId,
                        TargetType = request.TargetType,
                        TargetId = request.TargetId
                    })
                    .ToListAsync();

                // Đảo ngược để trả về thứ tự cũ -> mới
                messages.Reverse();

                var list = messages as System.Collections.Generic.List<ChatMessageDto> ?? messages.ToList();
                Console.WriteLine($"[Server] Returning {list.Count} history messages for '{request.TargetId}'.");
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
