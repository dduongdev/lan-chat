using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LanChat.Messaging;

namespace LanChat.Server.State
{
    public sealed class SessionManager
    {
        private readonly ConcurrentDictionary<string, SessionHandler> _sessions;

        public SessionManager()
        {
            _sessions = new ConcurrentDictionary<string, SessionHandler>(StringComparer.Ordinal);
        }

        public int Count => _sessions.Count;

        public void AddOrUpdateSession(string username, SessionHandler session)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username must not be empty.", nameof(username));
            }

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            // Nếu đã tồn tại, đóng session cũ (để thực hiện logic E2)
            if (_sessions.TryRemove(username, out var oldSession))
            {
                try
                {
                    Console.WriteLine($"[SessionManager] User '{username}' logged in from another location. Disconnecting old session.");
                    oldSession.TcpClient.Close(); 
                }
                catch { /* Ignore error on disconnect */ }
            }
            
            _sessions[username] = session;
        }

        public bool TryRemove(string username, out SessionHandler? session)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username must not be empty.", nameof(username));
            }

            return _sessions.TryRemove(username, out session);
        }

        public bool TryGet(string username, out SessionHandler? session)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username must not be empty.", nameof(username));
            }

            return _sessions.TryGetValue(username, out session);
        }

        public IEnumerable<string> GetOnlineUsernames()
        {
            var usernames = _sessions.Keys.ToList();
            usernames.Sort();
            return usernames;
        }

        public IReadOnlyCollection<KeyValuePair<string, SessionHandler>> Snapshot()
        {
            return new List<KeyValuePair<string, SessionHandler>>(_sessions);
        }

        public async Task HandleDisconnectAsync(SessionHandler session)
        {
            if (session == null) return;

            string? username = session.Username;

            // 1. Kiểm tra session có Username không (nếu chưa login thì thôi)
            if (!string.IsNullOrEmpty(username))
            {
                // 2. Xóa khỏi dictionary
                if (_sessions.TryRemove(username, out _))
                {
                    Console.WriteLine($"[SessionManager] User '{username}' disconnected. Cleaning up...");

                    // 3. Broadcast thông báo UserLeft
                    var leftPayload = new LanChat.Shared.Payloads.UserPresencePayload { Username = username };
                    
                    // Lặp qua tất cả session online khác để gửi thông báo
                    foreach (var otherSession in _sessions.Values)
                    {
                        try
                        {
                            await otherSession.SendAsync(LanChat.Shared.Constants.RoutingKeys.UserLeft, leftPayload);
                        }
                        catch { /* Bỏ qua nếu có lỗi gửi broadcast */ }
                    }
                }
            }

            // 4. Đóng kết nối TCP
            try
            {
                session.TcpClient.Close();
            }
            catch { /* Ignore */ }
        }

        public void Clear() => _sessions.Clear();
    }
}
