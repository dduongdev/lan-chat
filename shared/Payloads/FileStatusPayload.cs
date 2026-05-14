using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload cập nhật trạng thái truyền file (Client báo Server).
    /// RoutingKey: file.status
    /// </summary>
    public class FileStatusPayload
    {
        public Guid FileTransferId { get; set; }

        /// <summary>"Completed" hoặc "Failed"</summary>
        public string Status { get; set; } = string.Empty;
    }
}
