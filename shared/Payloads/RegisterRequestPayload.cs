namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload chứa thông tin người dùng gửi lên để đăng ký tài khoản.
    /// RoutingKey: auth.register.req
    /// </summary>
    public class RegisterRequestPayload
    {
        /// <summary>
        /// Tên đăng nhập người dùng muốn tạo.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Mật khẩu ở dạng văn bản thuần (plaintext).
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }
}
