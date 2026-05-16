using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
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
                ChatService.Instance.RaiseGroupInviteReceived(res);
            return Task.CompletedTask;
        }
    }
}
