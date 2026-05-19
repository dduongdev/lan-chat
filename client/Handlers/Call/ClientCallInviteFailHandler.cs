using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Call
{
    public sealed class ClientCallInviteFailHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.CallInviteFail;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<CallInviteFailPayload>();
            if (res != null) CallService.Instance.HandleInviteFail(res);
            return Task.CompletedTask;
        }
    }
}
