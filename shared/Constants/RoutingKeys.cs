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

        // Client -> Server: Yêu cầu đăng ký tài khoản
        public const string AuthRegisterReq = "auth.register.req";

        // Server -> Client: Phản hồi kết quả đăng ký
        public const string AuthRegisterRes = "auth.register.res";

        // Client -> Server: Yêu cầu đăng nhập
        public const string AuthLoginReq = "auth.login.req";

        // Server -> Client: Phản hồi kết quả đăng nhập
        public const string AuthLoginRes = "auth.login.res";

        // Server -> Client: Thông báo có người dùng mới online
        public const string UserJoined = "user.joined";

        // Server -> Client: Thông báo có người dùng offline
        public const string UserLeft = "user.left";

        // Client -> Server: Yêu cầu danh sách người dùng online
        public const string UserListReq = "user.list.req";

        // Server -> Client: Phản hồi danh sách người dùng online
        public const string UserListRes = "user.list.res";
    }
}
