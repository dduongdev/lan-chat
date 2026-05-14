using System;
using System.Linq;
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

namespace LanChat.Server.Handlers.Chat
{
    public class ServerChatHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;

        public string RoutingKey => RoutingKeys.ChatMsg;

        public ServerChatHandler(IServiceProvider serviceProvider, SessionManager sessionManager)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received ChatMsg from '{session.Username}'.");
            try
            {
                var request = payload.Deserialize<ChatMessagePayload>();
                if (request == null || string.IsNullOrWhiteSpace(request.Content))
                {
                    Console.WriteLine("[Server] ChatMsg payload is invalid.");
                    return;
                }

                if (string.IsNullOrEmpty(session.Username))
                {
                    Console.WriteLine("[Server] ChatMsg rejected: user not authenticated.");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Tìm SenderId từ Username
                var sender = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
                if (sender == null)
                {
                    Console.WriteLine($"[Server] Sender '{session.Username}' not found in DB.");
                    return;
                }

                // Tìm ReceiverId nếu là PRIVATE
                Guid? receiverId = null;
                if (request.TargetType == "PRIVATE")
                {
                    var receiver = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.TargetId);
                    if (receiver == null)
                    {
                        Console.WriteLine($"[Server] Target user '{request.TargetId}' not found in DB.");
                        return;
                    }
                    receiverId = receiver.Id;
                }

                Guid? groupId = null;
                if (request.TargetType == "GROUP")
                {
                    var group = await dbContext.ChatGroups.FirstOrDefaultAsync(u => u.Id == Guid.Parse(request.TargetId));
                    if (group == null)
                    {
                        Console.WriteLine($"[Server] Target group '{request.TargetId}' not found in DB.");
                        return;
                    }
                    groupId = group.Id;
                }

                // 1. Lưu trữ vào Database
                var serverMessageId = Guid.NewGuid();
                var sentAt = DateTime.UtcNow;
                var messageEntity = new Message
                {
                    Id = serverMessageId,
                    SenderId = sender.Id,
                    ReceiverId = receiverId,
                    GroupId = groupId, 
                    Content = request.Content,
                    SentAt = sentAt
                };

                dbContext.Messages.Add(messageEntity);
                await dbContext.SaveChangesAsync();

                Console.WriteLine($"[Server] Message saved to DB. ID={serverMessageId}");

                // 2. Gửi ACK (ChatEcho) cho người gửi
                var echoPayload = new ChatEchoPayload
                {
                    ClientMessageId = request.ClientMessageId,
                    ServerMessageId = serverMessageId,
                    SentAt = sentAt
                };
                await session.SendAsync(RoutingKeys.ChatEcho, echoPayload);

                // 3. Định tuyến tin nhắn
                var messageDto = new ChatMessageDto
                {
                    ServerMessageId = serverMessageId,
                    Sender = session.Username,
                    Content = request.Content,
                    SentAt = sentAt
                };

                switch (request.TargetType)
                {
                    case "PRIVATE":
                        // Gửi tới user đích nếu đang online
                        if (_sessionManager.TryGet(request.TargetId, out var targetSession) && targetSession != null)
                        {
                            await targetSession.SendAsync(RoutingKeys.ChatReceive, messageDto);
                            Console.WriteLine($"[Server] Message forwarded to '{request.TargetId}'.");
                        }
                        else
                        {
                            Console.WriteLine($"[Server] Target user '{request.TargetId}' is offline. Message stored in DB only.");
                        }
                        break;

                    case "ALL":
                        // Broadcast cho tất cả sessions trừ người gửi
                        foreach (var otherSession in _sessionManager.Snapshot())
                        {
                            if (otherSession.Key != session.Username)
                            {
                                try
                                {
                                    await otherSession.Value.SendAsync(RoutingKeys.ChatReceive, messageDto);
                                }
                                catch { /* Ignore failed broadcast */ }
                            }
                        }
                        Console.WriteLine("[Server] Message broadcasted to all users.");
                        break;

                    case "GROUP":
                        // Parse GroupId từ TargetId
                        if (!Guid.TryParse(request.TargetId, out var groupGuid))
                        {
                            Console.WriteLine($"[Server] Invalid GroupId format: {request.TargetId}");
                            break;
                        }

                        // Truy vấn danh sách thành viên trong nhóm (kèm Username)
                        var members = await dbContext.GroupMembers
                            .Where(gm => gm.GroupId == groupGuid)
                            .Include(gm => gm.User)
                            .ToListAsync();

                        if (members.Count == 0)
                        {
                            Console.WriteLine($"[Server] Group '{request.TargetId}' has no members or does not exist.");
                            break;
                        }


                        int delivered = 0;
                        foreach (var member in members)
                        {
                            // Bỏ qua người gửi
                            if (member.User.Username == session.Username) continue;

                            // Kiểm tra online và gửi
                            if (_sessionManager.TryGet(member.User.Username, out var memberSession) && memberSession != null)
                            {
                                try
                                {
                                    await memberSession.SendAsync(RoutingKeys.ChatReceive, messageDto);
                                    delivered++;
                                }
                                catch { /* Ignore failed delivery */ }
                            }
                        }
                        Console.WriteLine($"[Server] Group message delivered to {delivered}/{members.Count - 1} online members.");
                        break;

                    default:
                        Console.WriteLine($"[Server] Unknown TargetType: {request.TargetType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error processing ChatMsg: {ex.Message}");
            }
        }
    }
}
