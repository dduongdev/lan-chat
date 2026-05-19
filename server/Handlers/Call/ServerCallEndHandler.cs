using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.Call
{
    public sealed class ServerCallEndHandler : IMessageHandler
    {
        private readonly SessionManager _sessionManager;
        private readonly CallSessionManager _callSessionManager;

        public string RoutingKey => RoutingKeys.CallEnd;

        public ServerCallEndHandler(SessionManager sessionManager, CallSessionManager callSessionManager)
        {
            _sessionManager = sessionManager;
            _callSessionManager = callSessionManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            try
            {
                await HandleCoreAsync(session, payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Call end handler error: {ex.Message}");
            }
        }

        private async Task HandleCoreAsync(SessionHandler session, JsonElement payload)
        {
            if (string.IsNullOrEmpty(session.Username)) return;

            var request = payload.Deserialize<CallEndPayload>();
            if (request == null || request.CallId == Guid.Empty) return;

            if (!_callSessionManager.TryGet(request.CallId, out var call) || call == null) return;

            string username = session.Username;
            bool isParticipant;
            lock (call)
            {
                isParticipant = call.Participants.ContainsKey(username);
            }
            if (!isParticipant) return;

            if (call.TargetType == "PRIVATE")
            {
                var recipients = _callSessionManager.GetParticipantUsernames(request.CallId);
                await ServerCallNotifier.BroadcastEndedAsync(_callSessionManager, _sessionManager, request.CallId, request.Reason, recipients);
                _callSessionManager.EndCall(request.CallId);
                return;
            }

            var beforeRemove = _callSessionManager.GetParticipantUsernames(request.CallId);
            _callSessionManager.RemoveParticipant(request.CallId, username);
            var remaining = _callSessionManager.GetParticipantUsernames(request.CallId);

            await ServerCallNotifier.BroadcastParticipantLeftAsync(_sessionManager, beforeRemove, request.CallId, username, request.Reason);

            if (_callSessionManager.GetParticipantCount(request.CallId) < 2)
            {
                await ServerCallNotifier.BroadcastEndedAsync(_callSessionManager, _sessionManager, request.CallId, "NotEnoughParticipants", remaining);
                _callSessionManager.EndCall(request.CallId);
                return;
            }

            await ServerCallNotifier.BroadcastParticipantListAsync(_callSessionManager, _sessionManager, request.CallId);
        }
    }
}
