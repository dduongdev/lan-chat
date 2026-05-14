using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.Entities;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanChat.Server.Handlers.Chat
{
    public class ServerGroupAddHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;

        public string RoutingKey => RoutingKeys.GroupAddReq;

        public ServerGroupAddHandler(IServiceProvider serviceProvider, SessionManager sessionManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            if (string.IsNullOrEmpty(session.Username)) return;

            var req = payload.Deserialize<GroupAddRequestPayload>();
            if (req == null || req.GroupId == Guid.Empty || req.NewUsernames == null || !req.NewUsernames.Any())
            {
                await session.SendAsync(RoutingKeys.GroupAddRes, new GroupAddResponsePayload { Success = false, Message = "Invalid request." });
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var currentUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
            if (currentUser == null) return;

            // Kiểm tra quyền: session.UserId có trong group không
            var isMember = await dbContext.GroupMembers.AnyAsync(gm => gm.GroupId == req.GroupId && gm.UserId == currentUser.Id);
            if (!isMember)
            {
                await session.SendAsync(RoutingKeys.GroupAddRes, new GroupAddResponsePayload { Success = false, Message = "You are not a member of this group." });
                return;
            }

            var group = await dbContext.ChatGroups.Include(g => g.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(g => g.Id == req.GroupId);
            if (group == null)
            {
                await session.SendAsync(RoutingKeys.GroupAddRes, new GroupAddResponsePayload { Success = false, Message = "Group not found." });
                return;
            }

            // Lấy danh sách thành viên hiện tại
            var existingUsernames = group.Members.Select(m => m.User.Username).ToList();
            
            // Lọc ra các username chưa có trong nhóm
            var newMembers = req.NewUsernames.Except(existingUsernames).ToList();
            if (!newMembers.Any())
            {
                await session.SendAsync(RoutingKeys.GroupAddRes, new GroupAddResponsePayload { Success = false, Message = "All users are already in the group." });
                return;
            }

            var usersToAdd = await dbContext.Users.Where(u => newMembers.Contains(u.Username)).ToListAsync();
            
            foreach (var user in usersToAdd)
            {
                dbContext.GroupMembers.Add(new GroupMember
                {
                    GroupId = group.Id,
                    UserId = user.Id
                });
            }

            await dbContext.SaveChangesAsync();

            // Phản hồi cho người thêm
            await session.SendAsync(RoutingKeys.GroupAddRes, new GroupAddResponsePayload { Success = true, Message = "Users added successfully." });

            // Lấy danh sách thành viên MỚI sau khi thêm
            var allMembersNow = group.Members.Select(m => m.User.Username).Concat(usersToAdd.Select(u => u.Username)).ToList();

            var groupInfo = new GroupInfoDto
            {
                GroupId = group.Id,
                GroupName = group.GroupName,
                Creator = dbContext.Users.First(u => u.Id == group.CreatorId).Username,
                Members = allMembersNow
            };

            // Broadcast GroupInvite cho người mới
            foreach (var newUsername in usersToAdd.Select(u => u.Username))
            {
                if (_sessionManager.TryGet(newUsername, out var targetSession))
                {
                    await targetSession.SendAsync(RoutingKeys.GroupInvite, groupInfo);
                }
            }

            // Broadcast GroupMemberAdded cho người cũ (kể cả người vừa add)
            var addedPayload = new GroupMemberAddedPayload
            {
                GroupId = group.Id,
                NewUsernames = usersToAdd.Select(u => u.Username).ToList()
            };

            foreach (var existingUsername in existingUsernames)
            {
                if (_sessionManager.TryGet(existingUsername, out var oldSession))
                {
                    await oldSession.SendAsync(RoutingKeys.GroupMemberAdded, addedPayload);
                }
            }
        }
    }
}
