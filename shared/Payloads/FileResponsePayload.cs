using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload phản hồi lời mời nhận file từ Receiver gửi lên Server.
    /// RoutingKey: file.res
    /// </summary>
    public class FileResponsePayload
    {
        public Guid FileTransferId { get; set; }
        public bool Accepted { get; set; }
    }
}
