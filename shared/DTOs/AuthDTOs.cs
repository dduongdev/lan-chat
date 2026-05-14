namespace LanChat.Shared.DTOs
{
    /// <summary>
    /// Yêu cầu đăng nhập từ Client
    /// </summary>
    public class LoginRequest
    {
        /// <summary>Tên người dùng</summary>
        public string Username { get; set; } = string.Empty;
        
        /// <summary>Mật khẩu (nên được mã hóa trước khi gửi)</summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Phản hồi đăng nhập từ Server
    /// </summary>
    public class LoginResponse
    {
        /// <summary>Trạng thái đăng nhập (true = thành công)</summary>
        public bool Success { get; set; }
        
        /// <summary>Thông báo lỗi (nếu có)</summary>
        public string? Message { get; set; }
        
        /// <summary>ID của phiên kết nối</summary>
        public int SessionId { get; set; }
        
        /// <summary>ID người dùng</summary>
        public int UserId { get; set; }
        
        /// <summary>Khóa mã hóa AES được mã hóa RSA cho phiên này</summary>
        public string? EncryptedAesKey { get; set; }
    }

    /// <summary>
    /// Thông tin người dùng online
    /// </summary>
    public class UserInfo
    {
        /// <summary>ID người dùng</summary>
        public int UserId { get; set; }
        
        /// <summary>Tên người dùng</summary>
        public string Username { get; set; } = string.Empty;
        
        /// <summary>Trạng thái (online/offline/away)</summary>
        public string Status { get; set; } = "online";
    }
}
