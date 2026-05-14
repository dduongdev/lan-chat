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
    public class ServerGroupCreateHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;

        public string RoutingKey => RoutingKeys.GroupCreateReq;

        public ServerGroupCreateHandler(IServiceProvider serviceProvider, SessionManager sessionManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            if (string.IsNullOrEmpty(session.Username)) return;

            var req = payload.Deserialize<GroupCreateRequestPayload>();
            if (req == null || string.IsNullOrWhiteSpace(req.GroupName))
            {
                await session.SendAsync(RoutingKeys.GroupCreateRes, new GroupCreateResponsePayload { Success = false, Message = "Invalid group name." });
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var creator = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
            if (creator == null) return;

            var group = new ChatGroup
            {
                Id = Guid.NewGuid(),
                GroupName = req.GroupName,
                CreatorId = creator.Id
            };

            dbContext.ChatGroups.Add(group);

            // Add creator as member
            dbContext.GroupMembers.Add(new GroupMember
            {
                GroupId = group.Id,
                UserId = creator.Id
            });

            // Add initial members
            if (req.InitialMembers != null && req.InitialMembers.Any())
            {
                var membersToAdd = await dbContext.Users
                    .Where(u => req.InitialMembers.Contains(u.Username) && u.Username != session.Username)
                    .ToListAsync();

                foreach (var user in membersToAdd)
                {
                    dbContext.GroupMembers.Add(new GroupMember
                    {
                        GroupId = group.Id,
                        UserId = user.Id
                    });
                }
            }

            await dbContext.SaveChangesAsync();

            var resPayload = new GroupCreateResponsePayload
            {
                Success = true,
                Message = "Group created successfully.",
                GroupId = group.Id,
                GroupName = group.GroupName
            };

            await session.SendAsync(RoutingKeys.GroupCreateRes, resPayload);

            // Broadcast invite to other initial members
            var groupInfo = new GroupInfoDto
            {
                GroupId = group.Id,
                GroupName = group.GroupName,
                Creator = session.Username,
                Members = (req.InitialMembers ?? new System.Collections.Generic.List<string>())
                            .Concat(new[] { session.Username })
                            .Distinct()
                            .ToList()
            };

            if (req.InitialMembers != null)
            {
                foreach (var memberUsername in req.InitialMembers)
                {
                    if (memberUsername == session.Username) continue;
                    if (_sessionManager.TryGet(memberUsername, out var targetSession))
                    {
                        await targetSession.SendAsync(RoutingKeys.GroupInvite, groupInfo);
                    }
                }
            }
        }
    }
}
