using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.User
{
    public class ServerRecentChatsHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;

        public string RoutingKey => RoutingKeys.RecentChatsReq;

        public ServerRecentChatsHandler(IServiceProvider serviceProvider, SessionManager sessionManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            if (string.IsNullOrEmpty(session.Username)) return;

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var currentUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
            if (currentUser == null) return;

            var recentUserIds = await dbContext.Messages
                .Where(m => m.GroupId == null && m.ReceiverId != null && 
                            (m.SenderId == currentUser.Id || m.ReceiverId == currentUser.Id))
                .Select(m => m.SenderId == currentUser.Id ? m.ReceiverId!.Value : m.SenderId)
                .Distinct()
                .ToListAsync();

            var recentUsers = await dbContext.Users
                .Where(u => recentUserIds.Contains(u.Id))
                .Select(u => new RecentChatDto
                {
                    Username = u.Username,
                    DisplayName = u.Username,
                    IsOnline = false // Will be updated below
                })
                .ToListAsync();

            var onlineUsers = _sessionManager.Snapshot().Select(kvp => kvp.Value.Username).ToHashSet();

            foreach (var user in recentUsers)
            {
                user.IsOnline = onlineUsers.Contains(user.Username);
            }

            await session.SendAsync(RoutingKeys.RecentChatsRes, new RecentChatsResponsePayload
            {
                Users = recentUsers
            });
            
            int count = recentUsers.Count();
            Console.WriteLine($"[Server] Sent {count} recent chats to '{session.Username}'.");
        }
    }
}
