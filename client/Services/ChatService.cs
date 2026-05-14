using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SimpleTcp;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Services
{
    /// <summary>
    /// Singleton service quản lý kết nối TCP, Session và cung cấp sự kiện cho UI.
    /// Đây là cầu nối duy nhất giữa tầng WPF (UI Thread) và tầng Messaging (Background Thread).
    /// </summary>
    public class ChatService
    {
        private static ChatService? _instance;
        public static ChatService Instance => _instance ??= new ChatService();

        private SessionHandler? _session;
        private CancellationTokenSource? _cts;
        private Task? _sessionTask;
        private Task? _heartbeatTask;

        public SessionHandler? Session => _session;
        public bool IsConnected => _session != null;
        public string? CurrentUsername { get; set; }

        // ── Events cho UI ──────────────────────────────────────────────

        // Auth
        public event Action<bool, string>? OnRegisterResponse;
        public event Action<bool, string>? OnLoginResponse;

        // Presence
        public event Action<string>? OnUserJoined;
        public event Action<string>? OnUserLeft;
        public event Action<List<string>>? OnUserListReceived;

        // Chat
        public event Action<ChatMessageDto>? OnChatMessageReceived;
        public event Action<ChatEchoPayload>? OnChatEchoReceived;
        public event Action<List<ChatMessageDto>>? OnChatHistoryReceived;
        // Lưu TargetId kèm theo history để UI biết history này thuộc cuộc hội thoại nào
        public event Action<string, List<ChatMessageDto>>? OnChatHistoryWithTarget;

        // File
        public event Action<FileOfferPayload>? OnFileOfferReceived;
        public event Action<Guid, string, int>? OnFileStartReceived; // transferId, host, port
        public event Action<Guid, string>? OnFileStatusReceived; // transferId, status

        // Group
        public event Action<GroupCreateResponsePayload>? OnGroupCreateResponse;
        public event Action<List<GroupInfoDto>>? OnGroupListReceived;
        public event Action<GroupInfoDto>? OnGroupInviteReceived;
        public event Action<bool, string>? OnGroupAddResponse;
        public event Action<Guid, List<string>>? OnGroupMemberAdded;
        public event Action<bool, string>? OnGroupLeaveResponse;
        public event Action<Guid, string>? OnGroupMemberLeft;

        // Connection
        public event Action? OnDisconnected;
        public event Action? OnHandshakeCompleted;

        // ── Lưu trữ history target ──
        private string? _pendingHistoryTarget;

        private ChatService() { }

        // ── Kết nối & Handshake ─────────────────────────────────────────

        public async Task ConnectAsync(string host, int port)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port);

            var simpleClient = new SimpleTcpClient(tcpClient.Client);
            var dispatcher = CreateDispatcher();
            _session = new SessionHandler(simpleClient, dispatcher);
            _cts = new CancellationTokenSource();

            _sessionTask = Task.Run(async () =>
            {
                try
                {
                    await _session.StartAsync(_cts.Token);
                }
                catch { }
                finally
                {
                    OnDisconnected?.Invoke();
                }
            });

            // Gửi Handshake
            await _session.SendAsync(RoutingKeys.HandshakeReq, new { });
        }

        public void StartHeartbeat()
        {
            _heartbeatTask = Task.Run(async () =>
            {
                while (_session != null && _session.IsEncrypted)
                {
                    await Task.Delay(30000);
                    try
                    {
                        await _session.SendAsync(RoutingKeys.SysPing, new { });
                    }
                    catch { break; }
                }
            });
        }

        // ── Auth ────────────────────────────────────────────────────────

        public async Task RegisterAsync(string username, string password)
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.AuthRegisterReq, new RegisterRequestPayload
            {
                Username = username,
                Password = password
            });
        }

        public async Task LoginAsync(string username, string password)
        {
            if (_session == null) return;
            _session.Username = username;
            CurrentUsername = username;
            await _session.SendAsync(RoutingKeys.AuthLoginReq, new LoginRequestPayload
            {
                Username = username,
                Password = password
            });
        }

        public async Task LogoutAsync()
        {
            if (_session == null) return;
            try
            {
                await _session.SendAsync(RoutingKeys.AuthLogoutReq, new { });
                await Task.Delay(500);
            }
            catch { }
            _session.Stop();
            _cts?.Cancel();
            _session = null;
            CurrentUsername = null;
        }

        // ── User List ───────────────────────────────────────────────────

        public async Task RequestUserListAsync()
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.UserListReq, new { });
        }

        // ── Chat ────────────────────────────────────────────────────────

        public async Task SendPrivateMessageAsync(string targetUser, string content)
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.ChatMsg, new ChatMessagePayload
            {
                ClientMessageId = Guid.NewGuid(),
                TargetType = "PRIVATE",
                TargetId = targetUser,
                Content = content
            });
        }

        public async Task SendBroadcastMessageAsync(string content)
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.ChatMsg, new ChatMessagePayload
            {
                ClientMessageId = Guid.NewGuid(),
                TargetType = "ALL",
                TargetId = "",
                Content = content
            });
        }

        public async Task SendGroupMessageAsync(Guid groupId, string content)
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.ChatMsg, new ChatMessagePayload
            {
                ClientMessageId = Guid.NewGuid(),
                TargetType = "GROUP",
                TargetId = groupId.ToString(),
                Content = content
            });
        }

        public async Task RequestChatHistoryAsync(string targetId, int limit = 50)
        {
            if (_session == null) return;
            _pendingHistoryTarget = targetId;
            await _session.SendAsync(RoutingKeys.ChatHistoryReq, new ChatHistoryRequestPayload
            {
                TargetId = targetId,
                Limit = limit
            });
        }

        // ── File Transfer ───────────────────────────────────────────────

        public async Task SendFileRequestAsync(string receiverUsername, string filePath)
        {
            if (_session == null) return;
            var fileInfo = new System.IO.FileInfo(filePath);
            _session.SetMetadata("pending_file_path", filePath);
            await _session.SendAsync(RoutingKeys.FileRequest, new FileRequestPayload
            {
                ReceiverUsername = receiverUsername,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length
            });
        }

        public async Task RespondToFileOfferAsync(Guid fileTransferId, bool accepted)
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.FileResponse, new FileResponsePayload
            {
                FileTransferId = fileTransferId,
                Accepted = accepted
            });
        }

        // ── Group Management ────────────────────────────────────────────

        public async Task CreateGroupAsync(string groupName, List<string> initialMembers)
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.GroupCreateReq, new GroupCreateRequestPayload
            {
                GroupName = groupName,
                InitialMembers = initialMembers
            });
        }

        public async Task RequestGroupListAsync()
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.GroupListReq, new { });
        }

        public async Task AddGroupMembersAsync(Guid groupId, List<string> newUsernames)
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.GroupAddReq, new GroupAddRequestPayload
            {
                GroupId = groupId,
                NewUsernames = newUsernames
            });
        }

        public async Task LeaveGroupAsync(Guid groupId)
        {
            if (_session == null) return;
            await _session.SendAsync(RoutingKeys.GroupLeaveReq, new GroupLeaveRequestPayload
            {
                GroupId = groupId
            });
        }

        // ── Event Invokers (Gọi từ Handlers) ───────────────────────────

        public void RaiseRegisterResponse(bool success, string message) => OnRegisterResponse?.Invoke(success, message);
        public void RaiseLoginResponse(bool success, string message) => OnLoginResponse?.Invoke(success, message);
        public void RaiseUserJoined(string username) => OnUserJoined?.Invoke(username);
        public void RaiseUserLeft(string username) => OnUserLeft?.Invoke(username);
        public void RaiseUserListReceived(List<string> users) => OnUserListReceived?.Invoke(users);
        public void RaiseChatMessageReceived(ChatMessageDto msg) => OnChatMessageReceived?.Invoke(msg);
        public void RaiseChatEchoReceived(ChatEchoPayload echo) => OnChatEchoReceived?.Invoke(echo);
        public void RaiseChatHistoryReceived(List<ChatMessageDto> messages)
        {
            OnChatHistoryReceived?.Invoke(messages);
            if (_pendingHistoryTarget != null)
            {
                OnChatHistoryWithTarget?.Invoke(_pendingHistoryTarget, messages);
                _pendingHistoryTarget = null;
            }
        }
        public void RaiseFileOfferReceived(FileOfferPayload offer) => OnFileOfferReceived?.Invoke(offer);
        public void RaiseFileStartReceived(Guid transferId, string host, int port) => OnFileStartReceived?.Invoke(transferId, host, port);
        public void RaiseFileStatusReceived(Guid transferId, string status) => OnFileStatusReceived?.Invoke(transferId, status);
        public void RaiseGroupCreateResponse(GroupCreateResponsePayload res) => OnGroupCreateResponse?.Invoke(res);
        public void RaiseGroupListReceived(List<GroupInfoDto> groups) => OnGroupListReceived?.Invoke(groups);
        public void RaiseGroupInviteReceived(GroupInfoDto group) => OnGroupInviteReceived?.Invoke(group);
        public void RaiseGroupAddResponse(bool success, string message) => OnGroupAddResponse?.Invoke(success, message);
        public void RaiseGroupMemberAdded(Guid groupId, List<string> newMembers) => OnGroupMemberAdded?.Invoke(groupId, newMembers);
        public void RaiseGroupLeaveResponse(bool success, string message) => OnGroupLeaveResponse?.Invoke(success, message);
        public void RaiseGroupMemberLeft(Guid groupId, string username) => OnGroupMemberLeft?.Invoke(groupId, username);
        public void RaiseHandshakeCompleted() => OnHandshakeCompleted?.Invoke();

        // ── Dispatcher Setup ────────────────────────────────────────────

        private MessageDispatcher CreateDispatcher()
        {
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterHandler(new Handlers.ClientHandshakePubKeyHandler());
            dispatcher.RegisterHandler(new Handlers.ClientHandshakeDoneHandler());
            dispatcher.RegisterHandler(new Handlers.Auth.ClientRegisterResponseHandler());
            dispatcher.RegisterHandler(new Handlers.Auth.ClientLoginResponseHandler());
            dispatcher.RegisterHandler(new Handlers.Presence.ClientUserPresenceHandler(RoutingKeys.UserJoined));
            dispatcher.RegisterHandler(new Handlers.Presence.ClientUserPresenceHandler(RoutingKeys.UserLeft));
            dispatcher.RegisterHandler(new Handlers.User.ClientUserListResponseHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientChatEchoHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientChatReceiveHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientChatHistoryHandler());
            dispatcher.RegisterHandler(new Handlers.File.ClientFileOfferHandler());
            dispatcher.RegisterHandler(new Handlers.File.ClientFileStartHandler());
            dispatcher.RegisterHandler(new Handlers.File.ClientFileStatusHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientGroupCreateResponseHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientGroupListResponseHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientGroupInviteHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientGroupAddResponseHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientGroupMemberAddedHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientGroupLeaveResponseHandler());
            dispatcher.RegisterHandler(new Handlers.Chat.ClientGroupMemberLeftHandler());
            return dispatcher;
        }
    }
}
