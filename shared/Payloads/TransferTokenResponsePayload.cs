using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Server cấp "vé" (token) để Client kết nối vào cổng Data Stream (Port 8081).
    /// RoutingKey: file.transfer.res
    /// </summary>
    public class TransferTokenResponsePayload
    {
        /// <summary>Yêu cầu có được chấp thuận hay không.</summary>
        public bool Success { get; set; }

        /// <summary>"UPLOAD" hoặc "DOWNLOAD".</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>"Chìa khóa" dùng một lần trên Port 8081.</summary>
        public Guid TransferToken { get; set; }

        /// <summary>ID của file liên quan (để client theo dõi).</summary>
        public Guid FileId { get; set; }

        /// <summary>Thông báo lỗi (nếu Success = false).</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>UUID do Client tạo, Server gửi trả lại để Client xác định file tương ứng.</summary>
        public Guid ClientRequestId { get; set; }
    }
}
