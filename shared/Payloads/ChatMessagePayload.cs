using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload gửi tin nhắn từ Client lên Server.
    /// RoutingKey: chat.msg
    /// </summary>
    public class ChatMessagePayload
    {
        /// <summary>ID do Client sinh ra để khớp với ACK.</summary>
        public Guid ClientMessageId { get; set; }

        /// <summary>"PRIVATE", "GROUP", "ALL"</summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>Username (PRIVATE) hoặc GroupID (GROUP).</summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>Nội dung tin nhắn (Text/Emoji).</summary>
        public string Content { get; set; } = string.Empty;
    }
}
