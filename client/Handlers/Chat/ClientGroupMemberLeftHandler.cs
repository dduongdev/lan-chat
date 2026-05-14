using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupMemberLeftHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupMemberLeft;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<GroupMemberLeftPayload>();
            if (res != null)
            {
                Console.WriteLine($"[Client UI] Người dùng '{res.Username}' đã rời khỏi nhóm {res.GroupId}.");
            }
            return Task.CompletedTask;
        }
    }
}
