namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload chứa thông tin yêu cầu đăng nhập.
    /// RoutingKey: auth.login.req
    /// </summary>
    public class LoginRequestPayload
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
