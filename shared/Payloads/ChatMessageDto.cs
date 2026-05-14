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
    }
}
