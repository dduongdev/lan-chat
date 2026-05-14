using System.Collections.Generic;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload phản hồi lịch sử chat.
    /// RoutingKey: chat.his.res
    /// </summary>
    public class ChatHistoryResponsePayload
    {
        /// <summary>Username đối phương hoặc GroupID.</summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>Danh sách tin nhắn.</summary>
        public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
    }
}
