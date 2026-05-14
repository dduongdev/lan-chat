using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload lời mời nhận file từ Server gửi tới Receiver.
    /// RoutingKey: file.offer
    /// </summary>
    public class FileOfferPayload
    {
        public Guid FileTransferId { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }
}
