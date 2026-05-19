using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.Call
{
    public sealed class ServerCallMediaStateHandler : IMessageHandler
    {
        private readonly SessionManager _sessionManager;
        private readonly CallSessionManager _callSessionManager;

        public string RoutingKey => RoutingKeys.CallMediaState;

        public ServerCallMediaStateHandler(SessionManager sessionManager, CallSessionManager callSessionManager)
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
                Console.WriteLine($"Call media state handler error: {ex.Message}");
            }
        }

        private async Task HandleCoreAsync(SessionHandler session, JsonElement payload)
        {
            if (string.IsNullOrEmpty(session.Username)) return;

            var request = payload.Deserialize<CallMediaStatePayload>();
            if (request == null || request.CallId == Guid.Empty) return;

            if (!_callSessionManager.UpdateMediaState(
                    request.CallId,
                    session.Username,
                    request.CameraEnabled,
                    request.MicrophoneEnabled))
            {
                return;
            }

            foreach (var username in _callSessionManager.GetParticipantUsernames(request.CallId))
            {
                if (username == session.Username) continue;
                if (_sessionManager.TryGet(username, out var targetSession) && targetSession != null)
                {
                    try
                    {
                        await targetSession.SendAsync(RoutingKeys.CallMediaState, request);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Call media state send failed to {username}: {ex.Message}");
                    }
                }
            }
        }
    }
}
