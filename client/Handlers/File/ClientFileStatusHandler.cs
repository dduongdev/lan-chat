using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.File
{
    /// <summary>
    /// Xử lý thông báo trạng thái truyền file (từ Server).
    /// Ví dụ: khi Receiver từ chối, Server gửi FileStatusUpdate về cho Sender.
    /// </summary>
    public class ClientFileStatusHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.FileStatusUpdate;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var status = payload.Deserialize<FileStatusPayload>();
            if (status == null) return Task.CompletedTask;

            Console.WriteLine($"[Client UI] 📄 File transfer {status.FileTransferId}: Status = {status.Status}");

            return Task.CompletedTask;
        }
    }
}
