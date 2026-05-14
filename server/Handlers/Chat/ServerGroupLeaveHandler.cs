using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanChat.Server.Handlers.Chat
{
    public class ServerGroupLeaveHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;

        public string RoutingKey => RoutingKeys.GroupLeaveReq;

        public ServerGroupLeaveHandler(IServiceProvider serviceProvider, SessionManager sessionManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            if (string.IsNullOrEmpty(session.Username)) return;

            var req = payload.Deserialize<GroupLeaveRequestPayload>();
            if (req == null || req.GroupId == Guid.Empty)
            {
                await session.SendAsync(RoutingKeys.GroupLeaveRes, new GroupLeaveResponsePayload { Success = false, Message = "Invalid request." });
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var currentUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
            if (currentUser == null) return;

            var groupMember = await dbContext.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == req.GroupId && gm.UserId == currentUser.Id);
            if (groupMember == null)
            {
                await session.SendAsync(RoutingKeys.GroupLeaveRes, new GroupLeaveResponsePayload { Success = false, Message = "You are not a member of this group." });
                return;
            }

            dbContext.GroupMembers.Remove(groupMember);
            await dbContext.SaveChangesAsync();

            // Lấy thành viên còn lại
            var remainingMembers = await dbContext.GroupMembers
                .Where(gm => gm.GroupId == req.GroupId)
                .Include(gm => gm.User)
                .ToListAsync();

            if (remainingMembers.Count == 0)
            {
                // Xóa luôn nhóm nếu không còn ai
                var group = await dbContext.ChatGroups.FindAsync(req.GroupId);
                if (group != null)
                {
                    dbContext.ChatGroups.Remove(group);
                    await dbContext.SaveChangesAsync();
                }
            }

            await session.SendAsync(RoutingKeys.GroupLeaveRes, new GroupLeaveResponsePayload { Success = true, Message = "Left group successfully." });

            if (remainingMembers.Count > 0)
            {
                var leftPayload = new GroupMemberLeftPayload
                {
                    GroupId = req.GroupId,
                    Username = session.Username
                };

                foreach (var member in remainingMembers)
                {
                    if (_sessionManager.TryGet(member.User.Username, out var targetSession))
                    {
                        await targetSession.SendAsync(RoutingKeys.GroupMemberLeft, leftPayload);
                    }
                }
            }
        }
    }
}
