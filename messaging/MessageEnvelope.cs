using System;
using System.Text.Json;

namespace LanChat.Messaging
{
    public sealed class MessageEnvelope
    {
        public string RoutingKey { get; set; } = string.Empty;
        public JsonElement Payload { get; set; }

        public static MessageEnvelope Create<T>(string routingKey, T payload, JsonSerializerOptions? options = null)
        {
            if (routingKey == null)
            {
                throw new ArgumentNullException(nameof(routingKey));
            }

            JsonElement element = JsonSerializer.SerializeToElement(payload, options);

            return new MessageEnvelope
            {
                RoutingKey = routingKey,
                Payload = element
            };
        }
    }
}
