using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;

namespace LanChat.Client.Handlers
{
    public class ClientHandshakeDoneHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.HandshakeDone;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine("[Client] Secure Handshake completed successfully. Channel is now encrypted with AES.");
            ChatService.Instance.RaiseHandshakeCompleted();
            return Task.CompletedTask;
        }
    }
}
