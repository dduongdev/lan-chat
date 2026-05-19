using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Call
{
    public sealed class ClientCallInviteCreatedHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.CallInviteCreated;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<CallInviteCreatedPayload>();
            if (res != null) CallService.Instance.HandleInviteCreated(res);
            return Task.CompletedTask;
        }
    }
}
