using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientChatReceiveHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.ChatReceive;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var msg = payload.Deserialize<ChatMessageDto>();
            if (msg == null) return Task.CompletedTask;

            ChatService.Instance.RaiseChatMessageReceived(msg);
            return Task.CompletedTask;
        }
    }
}
