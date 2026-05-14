namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload cho trạng thái online/offline của user.
    /// RoutingKey: user.joined, user.left
    /// </summary>
    public class UserPresencePayload
    {
        public string Username { get; set; } = string.Empty;
    }
}
