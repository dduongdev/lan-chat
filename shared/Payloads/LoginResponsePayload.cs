namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload chứa kết quả phản hồi đăng nhập.
    /// RoutingKey: auth.login.res
    /// </summary>
    public class LoginResponsePayload
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
