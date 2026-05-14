using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientChatEchoHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.ChatEcho;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var echo = payload.Deserialize<ChatEchoPayload>();
            if (echo == null) return Task.CompletedTask;

            ChatService.Instance.RaiseChatEchoReceived(echo);
            return Task.CompletedTask;
        }
    }
}
