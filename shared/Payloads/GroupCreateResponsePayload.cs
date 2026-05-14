using System;

namespace LanChat.Shared.Payloads
{
    public class GroupCreateResponsePayload
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
    }
}
