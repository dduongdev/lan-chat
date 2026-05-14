using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Presence
{
    public class ClientUserPresenceHandler : IMessageHandler
    {
        private readonly string _routingKey;

        public string RoutingKey => _routingKey;

        public ClientUserPresenceHandler(string routingKey)
        {
            _routingKey = routingKey;
        }

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var presence = payload.Deserialize<UserPresencePayload>();
            if (presence == null) return Task.CompletedTask;

            if (_routingKey == RoutingKeys.UserJoined)
            {
                ChatService.Instance.RaiseUserJoined(presence.Username);
            }
            else if (_routingKey == RoutingKeys.UserLeft)
            {
                ChatService.Instance.RaiseUserLeft(presence.Username);
            }

            return Task.CompletedTask;
        }
    }
}
