using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload thông báo bắt đầu truyền file (Server gửi cho cả Sender và Receiver).
    /// RoutingKey: file.start
    /// </summary>
    public class FileStartPayload
    {
        public Guid FileTransferId { get; set; }
        public string TransferHost { get; set; } = string.Empty;
        public int TransferPort { get; set; }
    }
}
