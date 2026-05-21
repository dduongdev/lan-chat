using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Call
{
    public sealed class ClientCallParticipantListHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.CallParticipantList;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<CallParticipantListPayload>();
            if (res != null) CallService.Instance.HandleParticipantList(res);
            return Task.CompletedTask;
        }
    }
}
