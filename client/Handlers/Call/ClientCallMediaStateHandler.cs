using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Call
{
    public sealed class ClientCallMediaStateHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.CallMediaState;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<CallMediaStatePayload>();
            if (res != null) CallService.Instance.HandleMediaState(res);
            return Task.CompletedTask;
        }
    }
}
