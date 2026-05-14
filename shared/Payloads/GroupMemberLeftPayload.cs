using System;

namespace LanChat.Shared.Payloads
{
    public class GroupMemberLeftPayload
    {
        public Guid GroupId { get; set; }
        public string Username { get; set; } = string.Empty;
    }
}
