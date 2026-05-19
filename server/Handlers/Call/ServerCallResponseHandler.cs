using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;
using SimpleTcp;

namespace LanChat.Server.Handlers.Call
{
    public sealed class ServerCallResponseHandler : IMessageHandler
    {
        private readonly SessionManager _sessionManager;
        private readonly CallSessionManager _callSessionManager;

        public string RoutingKey => RoutingKeys.CallResponse;

        public ServerCallResponseHandler(SessionManager sessionManager, CallSessionManager callSessionManager)
        {
            _sessionManager = sessionManager;
            _callSessionManager = callSessionManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            if (string.IsNullOrEmpty(session.Username)) return;

            var request = payload.Deserialize<CallResponsePayload>();
            if (request == null || request.CallId == Guid.Empty) return;

            if (!_callSessionManager.TryGet(request.CallId, out var call) || call == null) return;

            string username = session.Username;
            lock (call)
            {
                if (!call.InvitedUsers.Contains(username)) return;
            }

            if (!request.Accept)
            {
                _callSessionManager.RejectParticipant(request.CallId, username);
                bool shouldEnd;
                lock (call)
                {
                    shouldEnd = call.InvitedUsers.Count > 0 && call.InvitedUsers.IsSubsetOf(call.RejectedUsers);
                }

                if (shouldEnd)
                {
                    var recipients = _callSessionManager.GetParticipantUsernames(request.CallId);
                    await ServerCallNotifier.BroadcastEndedAsync(_callSessionManager, _sessionManager, request.CallId, "Rejected", recipients);
                    _callSessionManager.EndCall(request.CallId);
                }
                return;
            }

            if (request.UdpPort <= 0 || request.UdpPort > 65535) return;

            bool added = _callSessionManager.AddParticipant(
                request.CallId,
                username,
                GetRemoteIp(session),
                request.UdpPort,
                request.CameraEnabled,
                request.MicrophoneEnabled);

            if (!added)
            {
                await session.SendAsync(RoutingKeys.CallEnded, new CallEndedPayload
                {
                    CallId = request.CallId,
                    Reason = "CallFull"
                });
                return;
            }

            await ServerCallNotifier.BroadcastParticipantListAsync(_callSessionManager, _sessionManager, request.CallId);
        }

        private static string GetRemoteIp(SessionHandler session)
        {
            if (session.TcpClient is SimpleTcpClient simpleClient &&
                simpleClient.Client.RemoteEndPoint is IPEndPoint endpoint)
            {
                return endpoint.Address.ToString();
            }

            return "127.0.0.1";
        }
    }
}
