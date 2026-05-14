using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload ACK trả về cho người gửi sau khi Server lưu tin nhắn thành công.
    /// RoutingKey: chat.echo
    /// </summary>
    public class ChatEchoPayload
    {
        /// <summary>ID trùng khớp với tin nhắn gốc do Client sinh.</summary>
        public Guid ClientMessageId { get; set; }

        /// <summary>ID tin nhắn do Server sinh ra sau khi lưu DB.</summary>
        public Guid ServerMessageId { get; set; }

        /// <summary>Thời gian Server ghi nhận.</summary>
        public DateTime SentAt { get; set; }
    }
}
