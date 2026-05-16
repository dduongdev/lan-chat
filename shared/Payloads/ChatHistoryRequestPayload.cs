using System;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload yêu cầu lịch sử chat.
    /// RoutingKey: chat.his.req
    /// </summary>
    public class ChatHistoryRequestPayload
    {
        /// <summary>Username đối phương (PRIVATE) hoặc GroupID.</summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>PRIVATE, GROUP, hoặc ALL</summary>
        public string TargetType { get; set; } = "PRIVATE";

        /// <summary>Lấy các tin nhắn trước thời điểm này (phân trang). Null = lấy mới nhất.</summary>
        public DateTime? BeforeTimestamp { get; set; }

        /// <summary>Số lượng tin nhắn tối đa trả về.</summary>
        public int Limit { get; set; } = 20;
    }
}
