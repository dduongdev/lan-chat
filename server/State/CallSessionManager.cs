using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LanChat.Shared.Payloads;

namespace LanChat.Server.State
{
    public sealed class CallParticipant
    {
        public ushort ParticipantId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int UdpPort { get; set; }
        public bool CameraEnabled { get; set; }
        public bool MicrophoneEnabled { get; set; }
        public DateTime JoinedAt { get; init; } = DateTime.UtcNow;
    }

    public sealed class CallSession
    {
        public Guid CallId { get; init; } = Guid.NewGuid();
        public string Caller { get; init; } = string.Empty;
        public string TargetType { get; init; } = string.Empty;
        public string TargetId { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public string Status { get; set; } = "Ringing";
        public Dictionary<string, CallParticipant> Participants { get; } = new(StringComparer.Ordinal);
        public HashSet<string> InvitedUsers { get; } = new(StringComparer.Ordinal);
        public HashSet<string> RejectedUsers { get; } = new(StringComparer.Ordinal);
        internal ushort NextParticipantId { get; set; } = 1;
    }

    public sealed class CallSessionManager
    {
        public const int MaxP2PMeshParticipants = 5;

        private readonly ConcurrentDictionary<Guid, CallSession> _activeCalls = new();

        public CallSession CreateCall(
            string caller,
            string targetType,
            string targetId,
            string callerIpAddress,
            int callerUdpPort,
            bool cameraEnabled,
            bool microphoneEnabled,
            IEnumerable<string> invitedUsers)
        {
            var call = new CallSession
            {
                CallId = Guid.NewGuid(),
                Caller = caller,
                TargetType = targetType,
                TargetId = targetId
            };

            foreach (var invited in invitedUsers.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                call.InvitedUsers.Add(invited);
            }

            call.Participants[caller] = new CallParticipant
            {
                ParticipantId = call.NextParticipantId++,
                Username = caller,
                IpAddress = callerIpAddress,
                UdpPort = callerUdpPort,
                CameraEnabled = cameraEnabled,
                MicrophoneEnabled = microphoneEnabled
            };

            _activeCalls[call.CallId] = call;
            return call;
        }

        public bool TryGet(Guid callId, out CallSession? call) => _activeCalls.TryGetValue(callId, out call);

        public bool AddParticipant(
            Guid callId,
            string username,
            string ipAddress,
            int udpPort,
            bool cameraEnabled,
            bool microphoneEnabled)
        {
            if (!_activeCalls.TryGetValue(callId, out var call)) return false;

            lock (call)
            {
                if (call.Participants.ContainsKey(username)) return true;
                if (call.Participants.Count >= MaxP2PMeshParticipants) return false;

                call.Participants[username] = new CallParticipant
                {
                    ParticipantId = call.NextParticipantId++,
                    Username = username,
                    IpAddress = ipAddress,
                    UdpPort = udpPort,
                    CameraEnabled = cameraEnabled,
                    MicrophoneEnabled = microphoneEnabled
                };
                call.Status = "Active";
                return true;
            }
        }

        public bool RejectParticipant(Guid callId, string username)
        {
            if (!_activeCalls.TryGetValue(callId, out var call)) return false;
            lock (call)
            {
                call.RejectedUsers.Add(username);
                return true;
            }
        }

        public bool UpdateMediaState(Guid callId, string username, bool cameraEnabled, bool microphoneEnabled)
        {
            if (!_activeCalls.TryGetValue(callId, out var call)) return false;
            lock (call)
            {
                if (!call.Participants.TryGetValue(username, out var participant)) return false;
                participant.CameraEnabled = cameraEnabled;
                participant.MicrophoneEnabled = microphoneEnabled;
                return true;
            }
        }

        public bool RemoveParticipant(Guid callId, string username)
        {
            if (!_activeCalls.TryGetValue(callId, out var call)) return false;
            lock (call)
            {
                return call.Participants.Remove(username);
            }
        }

        public bool EndCall(Guid callId)
        {
            if (!_activeCalls.TryRemove(callId, out var call)) return false;
            lock (call)
            {
                call.Status = "Ended";
            }
            return true;
        }

        public IReadOnlyList<CallParticipantDto> GetParticipantDtos(Guid callId)
        {
            if (!_activeCalls.TryGetValue(callId, out var call)) return Array.Empty<CallParticipantDto>();
            lock (call)
            {
                return call.Participants.Values
                    .OrderBy(p => p.ParticipantId)
                    .Select(p => new CallParticipantDto
                    {
                        ParticipantId = p.ParticipantId,
                        Username = p.Username,
                        IpAddress = p.IpAddress,
                        UdpPort = p.UdpPort,
                        CameraEnabled = p.CameraEnabled,
                        MicrophoneEnabled = p.MicrophoneEnabled
                    })
                    .ToList();
            }
        }

        public IReadOnlyList<string> GetParticipantUsernames(Guid callId)
        {
            if (!_activeCalls.TryGetValue(callId, out var call)) return Array.Empty<string>();
            lock (call)
            {
                return call.Participants.Keys.ToList();
            }
        }

        public int GetParticipantCount(Guid callId)
        {
            if (!_activeCalls.TryGetValue(callId, out var call)) return 0;
            lock (call)
            {
                return call.Participants.Count;
            }
        }

        public IReadOnlyList<Guid> RemoveUserFromAllCalls(string username)
        {
            var affected = new List<Guid>();
            foreach (var pair in _activeCalls.ToArray())
            {
                var call = pair.Value;
                lock (call)
                {
                    if (call.Participants.Remove(username))
                    {
                        affected.Add(call.CallId);
                    }
                }
            }
            return affected;
        }
    }
}
