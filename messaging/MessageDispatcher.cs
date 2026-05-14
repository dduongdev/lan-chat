using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LanChat.Messaging.Exceptions;
using SimpleTcp;

namespace LanChat.Messaging
{
    public sealed class MessageDispatcher
    {
        private readonly Dictionary<string, IMessageHandler> _handlers;

        public MessageDispatcher()
        {
            _handlers = new Dictionary<string, IMessageHandler>(StringComparer.Ordinal);
        }

        public void RegisterHandler(IMessageHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (string.IsNullOrWhiteSpace(handler.RoutingKey))
            {
                throw new ArgumentException("RoutingKey must not be empty.", nameof(handler));
            }

            _handlers[handler.RoutingKey] = handler;
        }

        public void RegisterHandlers(IEnumerable<IMessageHandler> handlers)
        {
            if (handlers == null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            foreach (IMessageHandler handler in handlers)
            {
                RegisterHandler(handler);
            }
        }

        public Task DispatchAsync(ISimpleTcpClient client, MessageEnvelope envelope)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (_handlers.TryGetValue(envelope.RoutingKey, out IMessageHandler? handler))
            {
                return handler.HandleAsync(client, envelope.Payload);
            }

            throw new UnsupportedRoutingKeyException(envelope.RoutingKey);
        }
    }
}
