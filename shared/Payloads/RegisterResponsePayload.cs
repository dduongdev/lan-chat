namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload chứa kết quả trả về khi người dùng đăng ký tài khoản.
    /// RoutingKey: auth.register.res
    /// </summary>
    public class RegisterResponsePayload
    {
        /// <summary>
        /// true nếu đăng ký thành công, false nếu thất bại.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Thông điệp chi tiết (VD: "Username đã tồn tại").
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
