using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Call
{
    public sealed class ClientCallParticipantLeftHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.CallParticipantLeft;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<CallParticipantLeftPayload>();
            if (res != null) CallService.Instance.HandleParticipantLeft(res);
            return Task.CompletedTask;
        }
    }
}
