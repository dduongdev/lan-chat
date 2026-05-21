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

        // Server -> Client: Phản hồi danh sách online
        public const string UserListRes = "user.list.res";

        // Client -> Server: Yêu cầu danh sách recent chats
        public const string RecentChatsReq = "user.recentchats.req";

        // Server -> Client: Phản hồi danh sách recent chats
        public const string RecentChatsRes = "user.recentchats.res";

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

        // ── UC-06 v2: Store & Forward File Transfer ──────────────────────

        // Client -> Server: Yêu cầu tải file lên
        public const string FileUploadReq = "file.upload.req";

        // Client -> Server: Yêu cầu tải file xuống
        public const string FileDownloadReq = "file.download.req";

        // Server -> Client: Phản hồi Token cho cả Upload và Download
        public const string FileTransferRes = "file.transfer.res";

        // ── Auth / Logout ────────────────────────────────────────────────

        // Client -> Server: Yêu cầu đăng xuất
        public const string AuthLogoutReq = "auth.logout.req";

        // ── Group Management ─────────────────────────────────────────────

        public const string GroupCreateReq  = "group.create.req";
        public const string GroupCreateRes  = "group.create.res";
        public const string GroupInvite     = "group.invite";

        // Lấy danh sách nhóm
        public const string GroupListReq    = "group.list.req";
        public const string GroupListRes    = "group.list.res";

        // Thêm thành viên
        public const string GroupAddReq     = "group.add.req";
        public const string GroupAddRes     = "group.add.res";
        public const string GroupMemberAdded = "group.member.added";

        // Rời nhóm
        public const string GroupLeaveReq   = "group.leave.req";
        public const string GroupLeaveRes   = "group.leave.res";
        public const string GroupMemberLeft = "group.member.left";

        // Client -> Server: Ping heartbeat
        public const string SysPing = "sys.ping";

        // ── UC-11: Video Call over UDP ─────────────────────────────────

        public const string CallInviteReq = "call.invite.req";
        public const string CallInviteCreated = "call.invite.created";
        public const string CallInviteIncoming = "call.invite.incoming";
        public const string CallInviteFail = "call.invite.fail";
        public const string CallResponse = "call.response";
        public const string CallParticipantList = "call.participant.list";
        public const string CallMediaState = "call.media.state";
        public const string CallParticipantLeft = "call.participant.left";
        public const string CallEnd = "call.end";
        public const string CallEnded = "call.ended";
    }
}
