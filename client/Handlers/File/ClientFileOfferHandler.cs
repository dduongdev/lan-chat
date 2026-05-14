using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.File
{
    /// <summary>
    /// Xử lý lời mời nhận file từ Server.
    /// Trong bản test này: tự động chấp nhận mọi file offer.
    /// Trong UI thực: sẽ hiển thị dialog cho người dùng chọn Accept/Reject.
    /// </summary>
    public class ClientFileOfferHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.FileOffer;

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var offer = payload.Deserialize<FileOfferPayload>();
            if (offer == null) return;

            double sizeMB = offer.FileSize / (1024.0 * 1024.0);
            Console.WriteLine($"[Client UI] 📁 '{offer.SenderUsername}' muốn gửi file '{offer.FileName}' ({sizeMB:F2} MB).");
            Console.WriteLine($"[Client UI] Tự động chấp nhận file transfer...");

            // Tự động chấp nhận (trong UI thực sẽ hỏi người dùng)
            var response = new FileResponsePayload
            {
                FileTransferId = offer.FileTransferId,
                Accepted = true
            };

            await session.SendAsync(RoutingKeys.FileResponse, response);

            // Lưu metadata để ClientFileStartHandler biết mình là Receiver
            session.SetMetadata($"file_role_{offer.FileTransferId}", "RECEIVER");
            session.SetMetadata($"file_name_{offer.FileTransferId}", offer.FileName);
            session.SetMetadata($"file_size_{offer.FileTransferId}", offer.FileSize.ToString());
        }
    }
}
