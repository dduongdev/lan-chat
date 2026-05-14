using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    /// <summary>
    /// Xử lý tin nhắn đến từ người dùng khác (được Server chuyển tiếp).
    /// </summary>
    public class ClientChatReceiveHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.ChatReceive;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var msg = payload.Deserialize<ChatMessageDto>();
            if (msg == null) return Task.CompletedTask;

            Console.WriteLine($"[Client UI] [{msg.SentAt:HH:mm:ss}] {msg.Sender}: {msg.Content}");
            // TODO: Thêm tin nhắn vào UI chat window

            return Task.CompletedTask;
        }
    }
}
