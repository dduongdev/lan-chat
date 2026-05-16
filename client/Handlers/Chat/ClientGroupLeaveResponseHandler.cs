using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupLeaveResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupLeaveRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<GroupLeaveResponsePayload>();
            if (res != null)
                ChatService.Instance.RaiseGroupLeaveResponse(res.Success, res.Message);
            return Task.CompletedTask;
        }
    }
}
