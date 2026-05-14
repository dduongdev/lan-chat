namespace LanChat.Shared.DTOs
{
    /// <summary>
    /// Tin nhắn chat được gửi từ Client
    /// </summary>
    public class ChatMessage
    {
        /// <summary>ID người gửi</summary>
        public int SenderId { get; set; }
        
        /// <summary>Tên người gửi</summary>
        public string SenderName { get; set; } = string.Empty;
        
        /// <summary>ID người nhận</summary>
        public int? ReceiverId { get; set; }
        
        /// <summary>Nội dung tin nhắn (có thể mã hóa AES)</summary>
        public string Content { get; set; } = string.Empty;
        
        /// <summary>Dấu thời gian gửi</summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>ID tin nhắn (cho ACK)</summary>
        public string MessageId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Xác nhận nhận tin nhắn
    /// </summary>
    public class ChatMessageAck
    {
        /// <summary>ID tin nhắn được xác nhận</summary>
        public string MessageId { get; set; } = string.Empty;
        
        /// <summary>Trạng thái: "received" hoặc "failed"</summary>
        public string Status { get; set; } = "received";
    }

    /// <summary>
    /// Yêu cầu lịch sử tin nhắn
    /// </summary>
    public class ChatHistoryRequest
    {
        /// <summary>ID người dùng muốn xem lịch sử với</summary>
        public int WithUserId { get; set; }
        
        /// <summary>Số lượng tin nhắn gần nhất cần lấy</summary>
        public int Limit { get; set; } = 50;
    }

    /// <summary>
    /// Phản hồi lịch sử tin nhắn
    /// </summary>
    public class ChatHistoryResponse
    {
        /// <summary>Danh sách tin nhắn</summary>
        public List<ChatMessage> Messages { get; set; } = new();
        
        /// <summary>Tổng số tin nhắn</summary>
        public int Total { get; set; }
    }
}
