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

        // Client -> Server: Gửi tin nhắn
        public const string ChatMsg = "chat.msg";

        // Server -> Sender: Xác nhận đã lưu (ACK)
        public const string ChatEcho = "chat.echo";

        // Server -> Receiver: Nhận tin nhắn
        public const string ChatReceive = "chat.recv";

        // Client -> Server: Yêu cầu lấy lịch sử chat
        public const string ChatHistoryReq = "chat.his.req";

        // Server -> Client: Phản hồi lịch sử chat
        public const string ChatHistoryRes = "chat.his.res";

        // Sender -> Server: Yêu cầu gửi file
        public const string FileRequest = "file.req";

        // Server -> Receiver: Lời mời nhận file
        public const string FileOffer = "file.offer";

        // Receiver -> Server: Phản hồi lời mời
        public const string FileResponse = "file.res";

        // Server -> Sender & Receiver: Bắt đầu truyền file
        public const string FileStartTransfer = "file.start";

        // Client -> Server: Báo cáo trạng thái hoàn tất/lỗi
        public const string FileStatusUpdate = "file.status";

        // Client -> Server: Yêu cầu đăng xuất
        public const string AuthLogoutReq = "auth.logout.req";

        // Client -> Server: Ping heartbeat
        public const string SysPing = "sys.ping";
    }
}
