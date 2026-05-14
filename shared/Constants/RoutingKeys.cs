namespace LanChat.Shared.Constants
{
    /// <summary>
    /// Định nghĩa các RoutingKey sử dụng trong hệ thống.
    /// Mỗi RoutingKey đại diện cho một loại yêu cầu/phản hồi cụ thể.
    /// </summary>
    public static class RoutingKeys
    {
        // ==================== Authentication ====================
        /// <summary>Client gửi: Yêu cầu đăng nhập</summary>
        public const string AuthLogin = "auth.login";
        
        /// <summary>Server gửi: Phản hồi đăng nhập</summary>
        public const string AuthLoginResponse = "auth.login.response";
        
        /// <summary>Server gửi: Thông báo người dùng mới online</summary>
        public const string AuthUserOnline = "auth.user.online";
        
        /// <summary>Server gửi: Thông báo người dùng offline</summary>
        public const string AuthUserOffline = "auth.user.offline";

        // ==================== Chat Messaging ====================
        /// <summary>Client gửi: Gửi tin nhắn chat</summary>
        public const string ChatSendMessage = "chat.send.message";
        
        /// <summary>Server gửi: Tin nhắn được ghi nhận</summary>
        public const string ChatMessageAck = "chat.message.ack";
        
        /// <summary>Server gửi: Phân phối tin nhắn đến các client khác</summary>
        public const string ChatReceiveMessage = "chat.receive.message";
        
        /// <summary>Client gửi: Yêu cầu lấy lịch sử tin nhắn</summary>
        public const string ChatGetHistory = "chat.get.history";
        
        /// <summary>Server gửi: Phản hồi lịch sử tin nhắn</summary>
        public const string ChatHistoryResponse = "chat.history.response";

        // ==================== File Transfer ====================
        /// <summary>Client gửi: Yêu cầu bắt đầu truyền file</summary>
        public const string FileTransferInitiate = "file.transfer.initiate";
        
        /// <summary>Server gửi: Chấp nhận hoặc từ chối truyền file</summary>
        public const string FileTransferResponse = "file.transfer.response";
        
        /// <summary>Client gửi: Dữ liệu file</summary>
        public const string FileTransferData = "file.transfer.data";
        
        /// <summary>Server gửi: Xác nhận nhận dữ liệu file</summary>
        public const string FileTransferAck = "file.transfer.ack";

        // ==================== User Status ====================
        /// <summary>Client gửi: Yêu cầu lấy danh sách user online</summary>
        public const string UserListRequest = "user.list.request";
        
        /// <summary>Server gửi: Danh sách user online hiện tại</summary>
        public const string UserListResponse = "user.list.response";
    }
}
