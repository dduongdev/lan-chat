using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupMemberAddedHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupMemberAdded;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<GroupMemberAddedPayload>();
            if (res != null)
            {
                Console.WriteLine($"[Client UI] Nhóm {res.GroupId} vừa có thêm thành viên mới: {string.Join(", ", res.NewUsernames)}");
            }
            return Task.CompletedTask;
        }
    }
}
