using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupAddResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupAddRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<GroupAddResponsePayload>();
            if (res != null)
                ChatService.Instance.RaiseGroupAddResponse(res.Success, res.Message);
            return Task.CompletedTask;
        }
    }
}
