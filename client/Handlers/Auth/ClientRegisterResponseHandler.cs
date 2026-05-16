using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Auth
{
    public class ClientRegisterResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.AuthRegisterRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var response = payload.Deserialize<RegisterResponsePayload>();
            if (response == null) return Task.CompletedTask;

            ChatService.Instance.RaiseRegisterResponse(response.Success, response.Message);
            return Task.CompletedTask;
        }
    }
}
