using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload yêu cầu gửi file từ Sender lên Server.
    /// RoutingKey: file.req
    /// </summary>
    public class FileRequestPayload
    {
        public string ReceiverUsername { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }
}
