using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Auth
{
    public class ClientLoginResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.AuthLoginRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var response = payload.Deserialize<LoginResponsePayload>();
            if (response == null) return Task.CompletedTask;

            ChatService.Instance.RaiseLoginResponse(response.Success, response.Message);
            return Task.CompletedTask;
        }
    }
}
