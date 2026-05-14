using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupCreateResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupCreateRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<LanChat.Shared.Payloads.GroupCreateResponsePayload>();
            if (res != null)
                ChatService.Instance.RaiseGroupCreateResponse(res);
            return Task.CompletedTask;
        }
    }
}
