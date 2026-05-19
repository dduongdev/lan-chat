using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpleTcp;

namespace LanChat.Server.Handlers.Call
{
    public sealed class ServerCallInviteHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;
        private readonly CallSessionManager _callSessionManager;

        public string RoutingKey => RoutingKeys.CallInviteReq;

        public ServerCallInviteHandler(
            IServiceProvider serviceProvider,
            SessionManager sessionManager,
            CallSessionManager callSessionManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
            _callSessionManager = callSessionManager;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            if (string.IsNullOrEmpty(session.Username))
            {
                await FailAsync(session, "Unauthorized", "You must login before starting a call.");
                return;
            }

            var request = payload.Deserialize<CallInviteRequestPayload>();
            if (request == null || string.IsNullOrWhiteSpace(request.TargetType) || string.IsNullOrWhiteSpace(request.TargetId))
            {
                await FailAsync(session, "InvalidRequest", "Invalid call invite request.");
                return;
            }

            if (request.UdpPort <= 0 || request.UdpPort > 65535)
            {
                await FailAsync(session, "InvalidUdpPort", "Invalid UDP port.");
                return;
            }

            string caller = session.Username;
            string callerIp = GetRemoteIp(session);

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (request.TargetType.Equals("PRIVATE", StringComparison.OrdinalIgnoreCase))
            {
                await HandlePrivateInviteAsync(session, request, caller, callerIp, dbContext);
                return;
            }

            if (request.TargetType.Equals("GROUP", StringComparison.OrdinalIgnoreCase))
            {
                await HandleGroupInviteAsync(session, request, caller, callerIp, dbContext);
                return;
            }

            await FailAsync(session, "UnsupportedTargetType", "Unsupported call target type.");
        }

        private async Task HandlePrivateInviteAsync(
            SessionHandler session,
            CallInviteRequestPayload request,
            string caller,
            string callerIp,
            AppDbContext dbContext)
        {
            var target = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.TargetId);
            if (target == null)
            {
                await FailAsync(session, "TargetNotFound", "Target user does not exist.");
                return;
            }

            if (!_sessionManager.TryGet(target.Username, out var targetSession) || targetSession == null)
            {
                await FailAsync(session, "TargetOffline", "Target user is offline.");
                return;
            }

            var call = _callSessionManager.CreateCall(
                caller,
                "PRIVATE",
                target.Username,
                callerIp,
                request.UdpPort,
                request.CameraEnabled,
                request.MicrophoneEnabled,
                new[] { target.Username });

            await session.SendAsync(RoutingKeys.CallInviteCreated, new CallInviteCreatedPayload
            {
                CallId = call.CallId,
                ParticipantId = call.Participants[caller].ParticipantId,
                MaxP2PMeshParticipants = CallSessionManager.MaxP2PMeshParticipants
            });

            await targetSession.SendAsync(RoutingKeys.CallInviteIncoming, new CallInviteIncomingPayload
            {
                CallId = call.CallId,
                Caller = caller,
                TargetType = "PRIVATE",
                TargetId = target.Username,
                CallerCameraEnabled = request.CameraEnabled,
                CallerMicrophoneEnabled = request.MicrophoneEnabled
            });
        }

        private async Task HandleGroupInviteAsync(
            SessionHandler session,
            CallInviteRequestPayload request,
            string caller,
            string callerIp,
            AppDbContext dbContext)
        {
            if (!Guid.TryParse(request.TargetId, out var groupId))
            {
                await FailAsync(session, "InvalidGroupId", "Invalid group id.");
                return;
            }

            var callerUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == caller);
            if (callerUser == null)
            {
                await FailAsync(session, "Unauthorized", "Caller does not exist.");
                return;
            }

            var isMember = await dbContext.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == callerUser.Id);
            if (!isMember)
            {
                await FailAsync(session, "Forbidden", "You are not a member of this group.");
                return;
            }

            var group = await dbContext.ChatGroups
                .Include(g => g.Members)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
            {
                await FailAsync(session, "GroupNotFound", "Group does not exist.");
                return;
            }

            var onlineMembers = group.Members
                .Select(m => m.User.Username)
                .Where(username => username != caller)
                .Where(username => _sessionManager.TryGet(username, out _))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (onlineMembers.Count == 0)
            {
                await FailAsync(session, "NoOnlineParticipants", "No group members are online.");
                return;
            }

            if (onlineMembers.Count + 1 > CallSessionManager.MaxP2PMeshParticipants)
            {
                await FailAsync(session, "TooManyParticipantsForP2P", "Too many online participants for UDP P2P mesh.");
                return;
            }

            var call = _callSessionManager.CreateCall(
                caller,
                "GROUP",
                groupId.ToString(),
                callerIp,
                request.UdpPort,
                request.CameraEnabled,
                request.MicrophoneEnabled,
                onlineMembers);

            await session.SendAsync(RoutingKeys.CallInviteCreated, new CallInviteCreatedPayload
            {
                CallId = call.CallId,
                ParticipantId = call.Participants[caller].ParticipantId,
                MaxP2PMeshParticipants = CallSessionManager.MaxP2PMeshParticipants
            });

            var incoming = new CallInviteIncomingPayload
            {
                CallId = call.CallId,
                Caller = caller,
                TargetType = "GROUP",
                TargetId = groupId.ToString(),
                CallerCameraEnabled = request.CameraEnabled,
                CallerMicrophoneEnabled = request.MicrophoneEnabled
            };

            foreach (var username in onlineMembers)
            {
                if (_sessionManager.TryGet(username, out var targetSession) && targetSession != null)
                {
                    await targetSession.SendAsync(RoutingKeys.CallInviteIncoming, incoming);
                }
            }
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

        private static Task FailAsync(SessionHandler session, string reason, string message)
        {
            return session.SendAsync(RoutingKeys.CallInviteFail, new CallInviteFailPayload
            {
                Reason = reason,
                Message = message
            });
        }
    }
}
