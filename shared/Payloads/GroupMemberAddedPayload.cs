using System;
using System.Collections.Generic;

namespace LanChat.Shared.Payloads
{
    public class GroupMemberAddedPayload
    {
        public Guid GroupId { get; set; }
        public List<string> NewUsernames { get; set; } = new List<string>();
    }
}
