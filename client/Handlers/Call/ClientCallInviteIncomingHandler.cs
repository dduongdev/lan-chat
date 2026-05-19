using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Call
{
    public sealed class ClientCallInviteIncomingHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.CallInviteIncoming;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<CallInviteIncomingPayload>();
            if (res != null) CallService.Instance.HandleIncomingCall(res);
            return Task.CompletedTask;
        }
    }
}
