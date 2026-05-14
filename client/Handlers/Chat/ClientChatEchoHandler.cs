using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    /// <summary>
    /// Xử lý ACK từ Server sau khi gửi tin nhắn thành công.
    /// </summary>
    public class ClientChatEchoHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.ChatEcho;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var echo = payload.Deserialize<ChatEchoPayload>();
            if (echo == null) return Task.CompletedTask;

            Console.WriteLine($"[Client UI] ACK: Tin nhắn đã gửi thành công. ServerID={echo.ServerMessageId}, SentAt={echo.SentAt:HH:mm:ss}");
            // TODO: Cập nhật trạng thái tin nhắn trong UI từ "Sending..." thành "Sent"

            return Task.CompletedTask;
        }
    }
}
