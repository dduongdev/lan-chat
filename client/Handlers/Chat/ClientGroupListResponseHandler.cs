using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
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
                ChatService.Instance.RaiseGroupListReceived(res.Groups);
            return Task.CompletedTask;
        }
    }
}
