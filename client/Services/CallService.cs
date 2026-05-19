using System;
using System.Linq;
using System.Threading.Tasks;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Services
{
    public sealed class CallService
    {
        private static CallService? _instance;
        public static CallService Instance => _instance ??= new CallService();

        private UdpMediaTransport? _transport;
        private Guid _callId;
        private ushort _participantId;
        private bool _isEnding;

        public event Action<CallInviteIncomingPayload>? IncomingCallReceived;
        public event Action<string>? CallStatusChanged;
        public event Action<ushort, byte[]>? RemoteVideoFrameReceived;
        public event Action<CallEndedPayload>? CallEnded;

        public bool HasActiveCall => _callId != Guid.Empty;
        public Guid ActiveCallId => _callId;

        private CallService() { }

        public void ClearEvents()
        {
            IncomingCallReceived = null;
            CallStatusChanged = null;
            RemoteVideoFrameReceived = null;
            CallEnded = null;
            ResetTransport();
        }

        public async Task StartPrivateCallAsync(string username)
        {
            await StartCallAsync("PRIVATE", username);
        }

        public async Task StartGroupCallAsync(Guid groupId)
        {
            await StartCallAsync("GROUP", groupId.ToString());
        }

        private async Task StartCallAsync(string targetType, string targetId)
        {
            var session = ChatService.Instance.Session;
            if (session == null) return;

            EnsureTransport();
            await session.SendAsync(RoutingKeys.CallInviteReq, new CallInviteRequestPayload
            {
                TargetType = targetType,
                TargetId = targetId,
                UdpPort = _transport!.LocalPort,
                CameraEnabled = true,
                MicrophoneEnabled = false
            });

            CallStatusChanged?.Invoke("Calling...");
        }

        public async Task AcceptCallAsync(CallInviteIncomingPayload invite)
        {
            var session = ChatService.Instance.Session;
            if (session == null) return;

            EnsureTransport();
            _callId = invite.CallId;

            await session.SendAsync(RoutingKeys.CallResponse, new CallResponsePayload
            {
                CallId = invite.CallId,
                Accept = true,
                UdpPort = _transport!.LocalPort,
                CameraEnabled = true,
                MicrophoneEnabled = false
            });

            CallStatusChanged?.Invoke($"Accepted call from {invite.Caller}");
        }

        public async Task RejectCallAsync(CallInviteIncomingPayload invite, string reason = "Rejected")
        {
            var session = ChatService.Instance.Session;
            if (session == null) return;

            await session.SendAsync(RoutingKeys.CallResponse, new CallResponsePayload
            {
                CallId = invite.CallId,
                Accept = false,
                Reason = reason
            });
        }

        public async Task EndCallAsync(string reason = "UserEnded")
        {
            if (_isEnding) return;
            _isEnding = true;

            try
            {
                var session = ChatService.Instance.Session;
                if (session != null && _callId != Guid.Empty)
                {
                    await session.SendAsync(RoutingKeys.CallEnd, new CallEndPayload
                    {
                        CallId = _callId,
                        Reason = reason
                    });
                }
            }
            finally
            {
                ResetTransport();
                _isEnding = false;
            }
        }

        public Task SendVideoFrameAsync(byte[] jpegBytes)
        {
            return _transport?.SendVideoFrameAsync(jpegBytes) ?? Task.CompletedTask;
        }

        public void HandleInviteCreated(CallInviteCreatedPayload payload)
        {
            _callId = payload.CallId;
            _participantId = payload.ParticipantId;
            _transport?.Configure(_callId, _participantId);
            CallStatusChanged?.Invoke($"Call created. Waiting for participants...");
        }

        public void HandleIncomingCall(CallInviteIncomingPayload payload)
        {
            IncomingCallReceived?.Invoke(payload);
        }

        public void HandleInviteFail(CallInviteFailPayload payload)
        {
            ResetTransport();
            CallStatusChanged?.Invoke($"Call failed: {payload.Message}");
        }

        public void HandleParticipantList(CallParticipantListPayload payload)
        {
            if (payload.CallId == Guid.Empty) return;

            _callId = payload.CallId;
            var currentUsername = ChatService.Instance.CurrentUsername;

            if (_participantId == 0)
            {
                var self = payload.Participants.FirstOrDefault(p => p.Username == currentUsername);
                if (self != null)
                {
                    _participantId = self.ParticipantId;
                }
            }

            if (_participantId == 0) return;

            EnsureTransport();
            _transport!.Configure(_callId, _participantId);
            _transport.UpdateParticipants(payload.Participants, currentUsername);
            CallStatusChanged?.Invoke($"Participants: {payload.Participants.Count}");
        }

        public void HandleParticipantLeft(CallParticipantLeftPayload payload)
        {
            CallStatusChanged?.Invoke($"{payload.Username} left the call.");
        }

        public void HandleCallEnded(CallEndedPayload payload)
        {
            ResetTransport();
            CallEnded?.Invoke(payload);
            CallStatusChanged?.Invoke($"Call ended: {payload.Reason}");
        }

        private void EnsureTransport()
        {
            if (_transport != null) return;

            _transport = new UdpMediaTransport();
            _transport.VideoFrameReceived += (participantId, frame) => RemoteVideoFrameReceived?.Invoke(participantId, frame);
            _transport.StatusChanged += status => CallStatusChanged?.Invoke(status);
            _transport.Open();

            if (_callId != Guid.Empty && _participantId != 0)
            {
                _transport.Configure(_callId, _participantId);
            }
        }

        private void ResetTransport()
        {
            _transport?.Dispose();
            _transport = null;
            _callId = Guid.Empty;
            _participantId = 0;
        }
    }
}
