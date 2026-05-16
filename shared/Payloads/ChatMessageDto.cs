using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Cấu trúc tin nhắn chuẩn dùng chung cho ChatReceive và ChatHistoryResponse.
    /// </summary>
    public class ChatMessageDto
    {
        /// <summary>ID tin nhắn trong DB.</summary>
        public Guid ServerMessageId { get; set; }

        /// <summary>Username người gửi.</summary>
        public string Sender { get; set; } = string.Empty;

        /// <summary>Nội dung.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Thời gian gửi.</summary>
        public DateTime SentAt { get; set; }

        /// <summary>Loại đích đến: PRIVATE, GROUP, ALL.</summary>
        public string TargetType { get; set; } = "PRIVATE";

        /// <summary>ID của người nhận hoặc nhóm (trống nếu ALL).</summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>Loại tin nhắn: "Text" hoặc "File".</summary>
        public string MessageType { get; set; } = "Text";

        /// <summary>ID file (chỉ có khi MessageType == "File").</summary>
        public Guid? FileId { get; set; }

        /// <summary>Tên file (chỉ có khi MessageType == "File").</summary>
        public string? FileName { get; set; }

        /// <summary>Kích thước file (chỉ có khi MessageType == "File").</summary>
        public long? FileSize { get; set; }

        /// <summary>SHA-256 hash (chỉ có khi MessageType == "File").</summary>
        public string? FileHash { get; set; }
    }
}
