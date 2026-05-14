using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.File
{
    /// <summary>
    /// Xử lý lời mời nhận file từ Server.
    /// Raise event để UI hiển thị dialog Accept/Reject.
    /// </summary>
    public class ClientFileOfferHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.FileOffer;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var offer = payload.Deserialize<FileOfferPayload>();
            if (offer == null) return Task.CompletedTask;

            // Lưu metadata để ClientFileStartHandler biết mình là Receiver
            session.SetMetadata($"file_role_{offer.FileTransferId}", "RECEIVER");
            session.SetMetadata($"file_name_{offer.FileTransferId}", offer.FileName);
            session.SetMetadata($"file_size_{offer.FileTransferId}", offer.FileSize.ToString());

            ChatService.Instance.RaiseFileOfferReceived(offer);
            return Task.CompletedTask;
        }
    }
}
