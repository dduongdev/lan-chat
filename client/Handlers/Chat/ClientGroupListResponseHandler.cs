using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupListResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupListRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<GroupListResponsePayload>();
            if (res != null)
            {
                Console.WriteLine($"[Client UI] Danh sách nhóm ({res.Groups.Count}):");
                foreach (var g in res.Groups)
                {
                    Console.WriteLine($"   - {g.GroupName} (Id: {g.GroupId}) [Creator: {g.Creator}] - {g.Members.Count} members");
                }
            }
            return Task.CompletedTask;
        }
    }
}
