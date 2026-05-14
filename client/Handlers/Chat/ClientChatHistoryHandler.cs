using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    /// <summary>
    /// Xử lý phản hồi lịch sử chat từ Server.
    /// </summary>
    public class ClientChatHistoryHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.ChatHistoryRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var response = payload.Deserialize<ChatHistoryResponsePayload>();
            if (response == null) return Task.CompletedTask;

            Console.WriteLine($"[Client UI] Lịch sử chat với '{response.TargetId}' ({response.Messages.Count} tin nhắn):");
            foreach (var msg in response.Messages)
            {
                Console.WriteLine($"   [{msg.SentAt:HH:mm:ss}] {msg.Sender}: {msg.Content}");
            }
            // TODO: Chèn danh sách tin nhắn vào đầu khung chat UI

            return Task.CompletedTask;
        }
    }
}
