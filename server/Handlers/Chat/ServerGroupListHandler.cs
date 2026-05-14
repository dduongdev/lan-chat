using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LanChat.Server.Handlers.Chat
{
    public class ServerGroupListHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;

        public string RoutingKey => RoutingKeys.GroupListReq;

        public ServerGroupListHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            if (string.IsNullOrEmpty(session.Username)) return;

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == session.Username);
            if (user == null) return;

            var groups = await dbContext.GroupMembers
                .Where(gm => gm.UserId == user.Id)
                .Include(gm => gm.Group)
                    .ThenInclude(g => g.Members)
                        .ThenInclude(m => m.User)
                .Select(gm => new GroupInfoDto
                {
                    GroupId = gm.GroupId,
                    GroupName = gm.Group.GroupName,
                    Creator = dbContext.Users.First(u => u.Id == gm.Group.CreatorId).Username,
                    Members = gm.Group.Members.Select(m => m.User.Username).ToList()
                })
                .ToListAsync();

            var resPayload = new GroupListResponsePayload
            {
                Groups = groups
            };

            await session.SendAsync(RoutingKeys.GroupListRes, resPayload);
        }
    }
}
