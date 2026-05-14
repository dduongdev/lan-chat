namespace LanChat.Shared.Constants
{
    /// <summary>
    /// Defines routing key constants used in the messaging system.
    /// Using a partial class so each feature can add its own keys in separate files.
    /// </summary>
    public static partial class RoutingKeys
    {
        // Client -> Server: Yêu cầu khóa công khai
        public const string HandshakeReq = "auth.handshake.req";

        // Server -> Client: Gửi trả khóa công khai
        public const string HandshakePubKey = "auth.handshake.pubkey";

        // Client -> Server: Gửi trả khóa AES đã được mã hóa
        public const string HandshakeRes = "auth.handshake.res";

        // Server -> Client: Thông báo Handshake thành công
        public const string HandshakeDone = "auth.handshake.done";
    }
}
