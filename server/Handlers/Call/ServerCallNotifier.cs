using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.Call
{
    internal static class ServerCallNotifier
    {
        public static async Task BroadcastParticipantListAsync(
            CallSessionManager callSessionManager,
            SessionManager sessionManager,
            Guid callId)
        {
            var payload = new CallParticipantListPayload
            {
                CallId = callId,
                Participants = callSessionManager.GetParticipantDtos(callId).ToList()
            };

            foreach (var username in callSessionManager.GetParticipantUsernames(callId))
            {
                if (sessionManager.TryGet(username, out var targetSession) && targetSession != null)
                {
                    await targetSession.SendAsync(RoutingKeys.CallParticipantList, payload);
                }
            }
        }

        public static async Task BroadcastEndedAsync(
            CallSessionManager callSessionManager,
            SessionManager sessionManager,
            Guid callId,
            string reason,
            IReadOnlyList<string>? recipients = null)
        {
            var usernames = recipients ?? callSessionManager.GetParticipantUsernames(callId);
            var payload = new CallEndedPayload { CallId = callId, Reason = reason };

            foreach (var username in usernames)
            {
                if (sessionManager.TryGet(username, out var targetSession) && targetSession != null)
                {
                    await targetSession.SendAsync(RoutingKeys.CallEnded, payload);
                }
            }
        }

        public static async Task BroadcastParticipantLeftAsync(
            SessionManager sessionManager,
            IReadOnlyList<string> recipients,
            Guid callId,
            string username,
            string reason)
        {
            var payload = new CallParticipantLeftPayload
            {
                CallId = callId,
                Username = username,
                Reason = reason
            };

            foreach (var recipient in recipients)
            {
                if (sessionManager.TryGet(recipient, out var targetSession) && targetSession != null)
                {
                    await targetSession.SendAsync(RoutingKeys.CallParticipantLeft, payload);
                }
            }
        }
    }
}
