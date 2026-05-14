using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientChatHistoryHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.ChatHistoryRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var response = payload.Deserialize<ChatHistoryResponsePayload>();
            if (response == null) return Task.CompletedTask;

            ChatService.Instance.RaiseChatHistoryReceived(response.Messages);
            return Task.CompletedTask;
        }
    }
}
