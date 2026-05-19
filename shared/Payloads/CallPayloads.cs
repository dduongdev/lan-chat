using System;
using System.Collections.Generic;

namespace LanChat.Shared.Payloads
{
    public sealed class CallInviteRequestPayload
    {
        public string TargetType { get; set; } = "PRIVATE";
        public string TargetId { get; set; } = string.Empty;
        public int UdpPort { get; set; }
        public bool CameraEnabled { get; set; } = true;
        public bool MicrophoneEnabled { get; set; } = true;
    }

    public sealed class CallInviteCreatedPayload
    {
        public Guid CallId { get; set; }
        public ushort ParticipantId { get; set; }
        public int MaxP2PMeshParticipants { get; set; }
    }

    public sealed class CallInviteIncomingPayload
    {
        public Guid CallId { get; set; }
        public string Caller { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public bool CallerCameraEnabled { get; set; }
        public bool CallerMicrophoneEnabled { get; set; }
    }

    public sealed class CallInviteFailPayload
    {
        public string Reason { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class CallResponsePayload
    {
        public Guid CallId { get; set; }
        public bool Accept { get; set; }
        public int UdpPort { get; set; }
        public bool CameraEnabled { get; set; } = true;
        public bool MicrophoneEnabled { get; set; } = true;
        public string? Reason { get; set; }
    }

    public sealed class CallParticipantDto
    {
        public ushort ParticipantId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int UdpPort { get; set; }
        public bool CameraEnabled { get; set; }
        public bool MicrophoneEnabled { get; set; }
    }

    public sealed class CallParticipantListPayload
    {
        public Guid CallId { get; set; }
        public List<CallParticipantDto> Participants { get; set; } = new();
    }

    public sealed class CallMediaStatePayload
    {
        public Guid CallId { get; set; }
        public bool CameraEnabled { get; set; }
        public bool MicrophoneEnabled { get; set; }
    }

    public sealed class CallParticipantLeftPayload
    {
        public Guid CallId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Reason { get; set; } = "Left";
    }

    public sealed class CallEndPayload
    {
        public Guid CallId { get; set; }
        public string Reason { get; set; } = "UserEnded";
    }

    public sealed class CallEndedPayload
    {
        public Guid CallId { get; set; }
        public string Reason { get; set; } = "Ended";
    }
}
