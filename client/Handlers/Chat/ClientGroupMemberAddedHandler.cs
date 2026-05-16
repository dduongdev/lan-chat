using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
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
                ChatService.Instance.RaiseGroupMemberAdded(res.GroupId, res.NewUsernames);
            return Task.CompletedTask;
        }
    }
}
