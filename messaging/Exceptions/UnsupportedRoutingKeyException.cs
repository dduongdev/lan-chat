using System;

namespace LanChat.Messaging.Exceptions
{
    public sealed class UnsupportedRoutingKeyException : Exception
    {
        public UnsupportedRoutingKeyException(string routingKey)
            : base($"RoutingKey '{routingKey}' is not supported.")
        {
        }
    }
}
