using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Call
{
    public sealed class ClientCallEndedHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.CallEnded;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<CallEndedPayload>();
            if (res != null) CallService.Instance.HandleCallEnded(res);
            return Task.CompletedTask;
        }
    }
}
