using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupInviteHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupInvite;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<GroupInfoDto>();
            if (res != null)
            {
                Console.WriteLine($"[Client UI] Bạn đã được thêm vào nhóm '{res.GroupName}' (Id: {res.GroupId}) bởi {res.Creator}.");
            }
            return Task.CompletedTask;
        }
    }
}
