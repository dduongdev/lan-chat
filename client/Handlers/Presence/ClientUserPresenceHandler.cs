using System;
using System.Text.Json;
using System.Threading.Tasks;
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
                Console.WriteLine($"[Client UI] User '{presence.Username}' has joined the chat.");
            }
            else if (_routingKey == RoutingKeys.UserLeft)
            {
                Console.WriteLine($"[Client UI] User '{presence.Username}' has left the chat.");
            }

            return Task.CompletedTask;
        }
    }
}
