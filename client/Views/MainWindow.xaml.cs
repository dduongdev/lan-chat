using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LanChat.Client.Services;
using LanChat.Shared.Payloads;
using Microsoft.Win32;

namespace LanChat.Client.Views
{
    // ── Data Models for UI ──
    public class SidebarItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isOnline;
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string SubText { get; set; } = "";
        public string Type { get; set; } = "USER"; // USER, GROUP, BROADCAST
        public Guid? GroupId { get; set; }
        public List<string>? Members { get; set; }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsOnline)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    public class MessageBubble
    {
        public string SenderLabel { get; set; } = "";
        public string Content { get; set; } = "";
        public string TimeText { get; set; } = "";
        public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;
        public Brush BubbleColor { get; set; } = new SolidColorBrush(Color.FromRgb(224, 224, 224));
        public Visibility ShowSender { get; set; } = Visibility.Visible;
        public Visibility ShowTimeRight { get; set; } = Visibility.Collapsed;
        public bool IsFile { get; set; } = false;
        public Guid? FileId { get; set; }
        public string? FileName { get; set; }
        public string? FileHash { get; set; }
        public System.Windows.Input.Cursor MessageCursor => IsFile ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
    }

    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<SidebarItem> _recentChats = new();
        private readonly ObservableCollection<SidebarItem> _otherOnlineUsers = new();
        private readonly ObservableCollection<SidebarItem> _groups = new();
        private readonly ObservableCollection<MessageBubble> _messages = new();

        private string? _currentChatType; // PRIVATE, GROUP, BROADCAST
        private string? _currentChatTarget; // Username, GroupId, or ALL
        private bool _isIntentionalClose = false;
        private CallWindow? _callWindow;

        private readonly Brush _sentBubble;
        private readonly Brush _receivedBubble;

        public MainWindow()
        {
            InitializeComponent();

            _sentBubble = (Brush)FindResource("SentBubbleBrush");
            _receivedBubble = (Brush)FindResource("ReceivedBubbleBrush");

            RecentChatsListBox.ItemsSource = _recentChats;
            UserListBox.ItemsSource = _otherOnlineUsers;
            GroupListBox.ItemsSource = _groups;
            MessagesPanel.ItemsSource = _messages;

            SubscribeEvents();
            LoadInitialData();
        }

        // ═══════════════════════════════════════════════════════════════
        // Event Subscriptions
        // ═══════════════════════════════════════════════════════════════

        private void SubscribeEvents()
        {
            var svc = ChatService.Instance;

            // User Presence
            svc.OnUserListReceived += users => Dispatcher.Invoke(() =>
            {
                _otherOnlineUsers.Clear();
                foreach (var u in users)
                {
                    if (u == svc.CurrentUsername) continue;
                    var recent = _recentChats.FirstOrDefault(r => r.Id == u);
                    if (recent != null)
                    {
                        recent.IsOnline = true;
                    }
                    else
                    {
                        _otherOnlineUsers.Add(new SidebarItem { Id = u, DisplayName = u, Type = "USER", IsOnline = true });
                    }
                }
                UpdateOnlineCount();
            });

            svc.OnRecentChatsReceived += chats => Dispatcher.Invoke(() =>
            {
                _recentChats.Clear();
                foreach (var c in chats)
                {
                    _recentChats.Add(new SidebarItem { Id = c.Username, DisplayName = c.DisplayName, Type = "USER", IsOnline = c.IsOnline });
                    var other = _otherOnlineUsers.FirstOrDefault(o => o.Id == c.Username);
                    if (other != null) _otherOnlineUsers.Remove(other);
                }
                UpdateOnlineCount();
            });

            svc.OnUserJoined += username => Dispatcher.Invoke(() =>
            {
                if (username == svc.CurrentUsername) return;
                var recent = _recentChats.FirstOrDefault(r => r.Id == username);
                if (recent != null)
                {
                    recent.IsOnline = true;
                }
                else if (!_otherOnlineUsers.Any(u => u.Id == username))
                {
                    _otherOnlineUsers.Add(new SidebarItem { Id = username, DisplayName = username, Type = "USER", IsOnline = true });
                }
                UpdateOnlineCount();
            });

            svc.OnUserLeft += username => Dispatcher.Invoke(() =>
            {
                var recent = _recentChats.FirstOrDefault(r => r.Id == username);
                if (recent != null) recent.IsOnline = false;
                
                var item = _otherOnlineUsers.FirstOrDefault(u => u.Id == username);
                if (item != null) _otherOnlineUsers.Remove(item);
                
                UpdateOnlineCount();
            });

            // Chat Messages
            svc.OnChatMessageReceived += msg => Dispatcher.Invoke(() =>
            {
                if (msg.TargetType == "PRIVATE")
                {
                    string otherUser = (msg.Sender == svc.CurrentUsername) ? msg.TargetId : msg.Sender;
                    if (!_recentChats.Any(r => r.Id == otherUser) && otherUser != svc.CurrentUsername)
                    {
                        var other = _otherOnlineUsers.FirstOrDefault(o => o.Id == otherUser);
                        if (other != null) _otherOnlineUsers.Remove(other);
                        _recentChats.Add(new SidebarItem { Id = otherUser, DisplayName = otherUser, Type = "USER", IsOnline = true });
                    }
                }

                // Nếu đang mở cuộc hội thoại tương ứng -> thêm tin nhắn vào
                bool isRelevant = false;
                if (msg.TargetType == "PRIVATE" && _currentChatType == "PRIVATE" && _currentChatTarget == (msg.Sender == svc.CurrentUsername ? msg.TargetId : msg.Sender)) isRelevant = true;
                else if (msg.TargetType == "GROUP" && _currentChatType == "GROUP" && _currentChatTarget == msg.TargetId) isRelevant = true;
                else if (msg.TargetType == "ALL" && _currentChatType == "BROADCAST") isRelevant = true;

                if (isRelevant)
                {
                    bool isMine = msg.Sender == svc.CurrentUsername;
                    AddMessageBubble(msg.Sender, msg.Content, msg.SentAt, isMine, msg.MessageType, msg.FileId, msg.FileName, msg.FileHash);
                }
            });

            svc.OnChatEchoReceived += echo => Dispatcher.Invoke(() =>
            {
                // ACK - tin nhắn đã gửi thành công (không cần hiển thị gì thêm)
            });

            svc.OnChatHistoryWithTarget += (targetId, messages) => Dispatcher.Invoke(() =>
            {
                _messages.Clear();
                foreach (var msg in messages)
                {
                    bool isMine = msg.Sender == svc.CurrentUsername;
                    AddMessageBubble(msg.Sender, msg.Content, msg.SentAt, isMine, msg.MessageType, msg.FileId, msg.FileName, msg.FileHash);
                }
                ScrollToBottom();
            });

            // Group Management
            svc.OnGroupListReceived += groups => Dispatcher.Invoke(() =>
            {
                _groups.Clear();
                foreach (var g in groups)
                {
                    _groups.Add(new SidebarItem
                    {
                        Id = g.GroupId.ToString(),
                        DisplayName = g.GroupName,
                        SubText = $"{g.Members.Count} thành viên",
                        Type = "GROUP",
                        GroupId = g.GroupId,
                        Members = g.Members
                    });
                }
            });

            svc.OnGroupInviteReceived += group => Dispatcher.Invoke(() =>
            {
                if (!_groups.Any(g => g.Id == group.GroupId.ToString()))
                {
                    _groups.Add(new SidebarItem
                    {
                        Id = group.GroupId.ToString(),
                        DisplayName = group.GroupName,
                        SubText = $"{group.Members.Count} thành viên",
                        Type = "GROUP",
                        GroupId = group.GroupId,
                        Members = group.Members
                    });
                }
                MessageBox.Show($"Bạn đã được thêm vào nhóm \"{group.GroupName}\"", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            });

            svc.OnGroupCreateResponse += res => Dispatcher.Invoke(() =>
            {
                if (res.Success)
                    svc.RequestGroupListAsync(); // Reload group list
                else
                    MessageBox.Show($"Tạo nhóm thất bại: {res.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            });

            svc.OnGroupMemberAdded += (groupId, newMembers) => Dispatcher.Invoke(() =>
            {
                var group = _groups.FirstOrDefault(g => g.Id == groupId.ToString());
                if (group?.Members != null)
                {
                    group.Members.AddRange(newMembers);
                    group.SubText = $"{group.Members.Count} thành viên";
                }
                if (_currentChatType == "GROUP" && _currentChatTarget == groupId.ToString())
                    ChatSubHeaderText.Text = $"{group?.Members?.Count ?? 0} thành viên";
            });

            svc.OnGroupMemberLeft += (groupId, username) => Dispatcher.Invoke(() =>
            {
                var group = _groups.FirstOrDefault(g => g.Id == groupId.ToString());
                if (group?.Members != null)
                {
                    group.Members.Remove(username);
                    group.SubText = $"{group.Members.Count} thành viên";
                }
                if (_currentChatType == "GROUP" && _currentChatTarget == groupId.ToString())
                    ChatSubHeaderText.Text = $"{group?.Members?.Count ?? 0} thành viên";
            });

            svc.OnGroupLeaveResponse += (success, message) => Dispatcher.Invoke(() =>
            {
                if (success)
                {
                    // Xóa nhóm khỏi sidebar
                    var group = _groups.FirstOrDefault(g => g.Id == _currentChatTarget);
                    if (group != null) _groups.Remove(group);
                    ClearChatArea();
                }
                else
                {
                    MessageBox.Show($"Rời nhóm thất bại: {message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });

            // UC-06 v2: File Transfer
            svc.OnFileUploadStarted += fileId => Dispatcher.Invoke(() =>
            {
                AddSystemMessage("📤 Đang tải file lên Server...");
            });

            svc.OnFileDownloadCompleted += (fileId, savePath) => Dispatcher.Invoke(() =>
            {
                AddSystemMessage($"✅ Tải file hoàn tất! Lưu tại: {savePath}");
            });

            svc.OnFileTransferError += (fileId, error) => Dispatcher.Invoke(() =>
            {
                AddSystemMessage($"❌ Lỗi truyền file: {error}");
            });

            // Disconnected
            svc.OnDisconnected += () => Dispatcher.Invoke(() =>
            {
                if (!_isIntentionalClose)
                {
                    ChatService.Instance.ClearEvents();
                    MessageBox.Show("Đã mất kết nối đến server.", "Ngắt kết nối", MessageBoxButton.OK, MessageBoxImage.Warning);
                    var login = new LoginWindow();
                    login.Show();
                    this.Close();
                }
            });

            CallService.Instance.IncomingCallReceived += invite => Dispatcher.Invoke(async () =>
            {
                var result = MessageBox.Show(
                    $"{invite.Caller} đang gọi video. Bạn muốn nhận cuộc gọi?",
                    "Cuộc gọi video",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await CallService.Instance.AcceptCallAsync(invite);
                    ShowCallWindow($"Video call with {invite.Caller}");
                }
                else
                {
                    await CallService.Instance.RejectCallAsync(invite);
                }
            });

            CallService.Instance.CallStatusChanged += status => Dispatcher.Invoke(() =>
            {
                if (status.StartsWith("Call failed:", StringComparison.Ordinal))
                {
                    MessageBox.Show(status, "Video call", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // Initial Data Load
        // ═══════════════════════════════════════════════════════════════

        private async void LoadInitialData()
        {
            ChatService.Instance.StartHeartbeat();
            await ChatService.Instance.RequestRecentChatsAsync();
            await ChatService.Instance.RequestUserListAsync();
            await ChatService.Instance.RequestGroupListAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // Sidebar Selection Handlers
        // ═══════════════════════════════════════════════════════════════

        private void BroadcastButton_Click(object sender, RoutedEventArgs e)
        {
            RecentChatsListBox.SelectedItem = null;
            UserListBox.SelectedItem = null;
            GroupListBox.SelectedItem = null;
            SwitchChat("BROADCAST", "ALL", "#general", "AES Encrypted");
            VideoCallButton.Visibility = Visibility.Collapsed;
            SendFileButton.Visibility = Visibility.Collapsed;
            AddMemberButton.Visibility = Visibility.Collapsed;
            LeaveGroupButton.Visibility = Visibility.Collapsed;
        }

        private void UserListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem is SidebarItem selectedUser)
            {
                if (listBox == UserListBox) RecentChatsListBox.SelectedItem = null;
                else if (listBox == RecentChatsListBox) UserListBox.SelectedItem = null;

                GroupListBox.SelectedItem = null;
                SwitchChat("PRIVATE", selectedUser.Id, selectedUser.DisplayName, "Trò chuyện riêng tư");
                VideoCallButton.Visibility = Visibility.Visible;
                SendFileButton.Visibility = Visibility.Visible;
                AddMemberButton.Visibility = Visibility.Collapsed;
                LeaveGroupButton.Visibility = Visibility.Collapsed;
            }
        }

        private void GroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupListBox.SelectedItem is SidebarItem item)
            {
                RecentChatsListBox.SelectedItem = null;
                UserListBox.SelectedItem = null;
                SwitchChat("GROUP", item.Id, $"#{item.DisplayName}", $"AES Encrypted · {item.SubText}");
                VideoCallButton.Visibility = Visibility.Visible;
                SendFileButton.Visibility = Visibility.Visible;
                AddMemberButton.Visibility = Visibility.Visible;
                LeaveGroupButton.Visibility = Visibility.Visible;
            }
        }

        private async void SwitchChat(string type, string target, string header, string subHeader)
        {
            _currentChatType = type;
            _currentChatTarget = target;
            ChatHeaderText.Text = header;
            ChatSubHeaderText.Text = subHeader;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            MessageInputBox.IsEnabled = true;
            SendButton.IsEnabled = true;
            _messages.Clear();

            // Load history
            await ChatService.Instance.RequestChatHistoryAsync(type, target, 50);
        }

        private void ClearChatArea()
        {
            _currentChatType = null;
            _currentChatTarget = null;
            ChatHeaderText.Text = "Chọn một cuộc hội thoại";
            ChatSubHeaderText.Text = "";
            EmptyStatePanel.Visibility = Visibility.Visible;
            MessageInputBox.IsEnabled = false;
            SendButton.IsEnabled = false;
            VideoCallButton.Visibility = Visibility.Collapsed;
            SendFileButton.Visibility = Visibility.Collapsed;
            AddMemberButton.Visibility = Visibility.Collapsed;
            LeaveGroupButton.Visibility = Visibility.Collapsed;
            _messages.Clear();
        }

        // ═══════════════════════════════════════════════════════════════
        // Send Message
        // ═══════════════════════════════════════════════════════════════

        private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendCurrentMessage();

        private async void MessageInputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                await SendCurrentMessage();
        }

        private async System.Threading.Tasks.Task SendCurrentMessage()
        {
            string text = MessageInputBox.Text.Trim();
            if (string.IsNullOrEmpty(text) || _currentChatType == null) return;

            MessageInputBox.Text = "";

            switch (_currentChatType)
            {
                case "PRIVATE":
                    if (!_recentChats.Any(r => r.Id == _currentChatTarget))
                    {
                        var other = _otherOnlineUsers.FirstOrDefault(o => o.Id == _currentChatTarget);
                        if (other != null) 
                        {
                            _otherOnlineUsers.Remove(other);
                            _recentChats.Add(other);
                        }
                    }
                    await ChatService.Instance.SendPrivateMessageAsync(_currentChatTarget!, text);
                    break;
                case "BROADCAST":
                    await ChatService.Instance.SendBroadcastMessageAsync(text);
                    break;
                case "GROUP":
                    if (Guid.TryParse(_currentChatTarget, out var gid))
                        await ChatService.Instance.SendGroupMessageAsync(gid, text);
                    break;
            }

            // Thêm tin nhắn của mình vào UI ngay lập tức
            AddMessageBubble(ChatService.Instance.CurrentUsername ?? "Me", text, DateTime.UtcNow, true);
        }

        // ═══════════════════════════════════════════════════════════════
        // Action Buttons
        // ═══════════════════════════════════════════════════════════════

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn muốn đăng xuất?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _isIntentionalClose = true;
                ChatService.Instance.ClearEvents();
                await ChatService.Instance.LogoutAsync();
                
                var login = new LoginWindow();
                login.Show();
                
                // Đóng dứt điểm
                this.Close();
            }
        }

        private void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentChatTarget == null) return;

            var dialog = new OpenFileDialog
            {
                Title = "Chọn file để gửi",
                Filter = "Tất cả file (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                if (_currentChatType == "PRIVATE")
                {
                    _ = ChatService.Instance.SendFileUploadRequestAsync(_currentChatTarget, dialog.FileName);
                }
                else if (_currentChatType == "GROUP" && Guid.TryParse(_currentChatTarget, out var gid))
                {
                    _ = ChatService.Instance.SendGroupFileUploadRequestAsync(gid, dialog.FileName);
                }
                AddSystemMessage($"📤 Đang gửi file: {System.IO.Path.GetFileName(dialog.FileName)}...");
            }
        }

        private async void VideoCallButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentChatTarget == null || _currentChatType == null) return;

            if (_currentChatType == "PRIVATE")
            {
                await CallService.Instance.StartPrivateCallAsync(_currentChatTarget);
                ShowCallWindow($"Video call with {_currentChatTarget}");
            }
            else if (_currentChatType == "GROUP" && Guid.TryParse(_currentChatTarget, out var groupId))
            {
                await CallService.Instance.StartGroupCallAsync(groupId);
                ShowCallWindow($"Video call: {ChatHeaderText.Text}");
            }
        }

        private void CreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var combinedUsers = _recentChats.Select(u => u.DisplayName)
                .Concat(_otherOnlineUsers.Select(u => u.DisplayName))
                .Distinct()
                .ToList();
                
            var dialog = new CreateGroupDialog(combinedUsers);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _ = ChatService.Instance.CreateGroupAsync(dialog.GroupName, dialog.SelectedMembers);
            }
        }

        private void AddMemberButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentChatType != "GROUP" || _currentChatTarget == null) return;

            var currentGroup = _groups.FirstOrDefault(g => g.Id == _currentChatTarget);
            var existingMembers = currentGroup?.Members ?? new List<string>();

            // Hiển thị danh sách user chưa có trong nhóm
            var availableUsers = _recentChats.Select(u => u.DisplayName)
                .Concat(_otherOnlineUsers.Select(u => u.DisplayName))
                .Distinct()
                .Where(u => !existingMembers.Contains(u))
                .ToList();

            if (!availableUsers.Any())
            {
                MessageBox.Show("Không có người dùng online nào để thêm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new AddMemberDialog(availableUsers);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.SelectedMembers.Any())
            {
                if (Guid.TryParse(_currentChatTarget, out var gid))
                    _ = ChatService.Instance.AddGroupMembersAsync(gid, dialog.SelectedMembers);
            }
        }

        private async void LeaveGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentChatType != "GROUP" || _currentChatTarget == null) return;

            var result = MessageBox.Show("Bạn chắc chắn muốn rời nhóm?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                if (Guid.TryParse(_currentChatTarget, out var gid))
                    await ChatService.Instance.LeaveGroupAsync(gid);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════

        private void AddMessageBubble(string sender, string content, DateTime sentAt, bool isMine, string messageType = "Text", Guid? fileId = null, string? fileName = null, string? fileHash = null)
        {
            _messages.Add(new MessageBubble
            {
                SenderLabel = sender,
                Content = content,
                TimeText = sentAt.ToLocalTime().ToString("h:mm tt"),
                Alignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                BubbleColor = isMine ? _sentBubble : _receivedBubble,
                ShowSender = isMine ? Visibility.Collapsed : Visibility.Visible,
                ShowTimeRight = isMine ? Visibility.Visible : Visibility.Collapsed,
                IsFile = messageType == "File",
                FileId = fileId,
                FileName = fileName,
                FileHash = fileHash
            });
            ScrollToBottom();
        }

        private async void MessageBubble_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is MessageBubble bubble)
            {
                if (bubble.IsFile && bubble.FileId.HasValue && !string.IsNullOrEmpty(bubble.FileName) && !string.IsNullOrEmpty(bubble.FileHash))
                {
                    await ChatService.Instance.SendFileDownloadRequestAsync(bubble.FileId.Value, bubble.FileName, bubble.FileHash);
                }
            }
        }

        private void AddSystemMessage(string text)
        {
            _messages.Add(new MessageBubble
            {
                SenderLabel = "",
                Content = text,
                TimeText = DateTime.Now.ToString("HH:mm"),
                Alignment = HorizontalAlignment.Center,
                BubbleColor = new SolidColorBrush(Colors.Transparent),
                ShowSender = Visibility.Collapsed
            });
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            MessageScrollViewer.ScrollToEnd();
        }

        private void UpdateOnlineCount()
        {
            int onlineCount = _otherOnlineUsers.Count + _recentChats.Count(r => r.IsOnline);
            OnlineCountText.Text = $"Online Users: {onlineCount}";
        }

        private void ShowCallWindow(string title)
        {
            if (_callWindow != null && _callWindow.IsVisible)
            {
                _callWindow.Activate();
                return;
            }

            _callWindow = new CallWindow(title)
            {
                Owner = this
            };
            _callWindow.Closed += (_, _) => _callWindow = null;
            _callWindow.Show();
        }
    }
}
