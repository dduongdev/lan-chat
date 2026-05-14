using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        public bool TryAdd(string username, SessionHandler session)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username must not be empty.", nameof(username));
            }

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return _sessions.TryAdd(username, session);
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

        public IReadOnlyCollection<KeyValuePair<string, SessionHandler>> Snapshot()
        {
            return new List<KeyValuePair<string, SessionHandler>>(_sessions);
        }

        public void Clear() => _sessions.Clear();
    }
}
