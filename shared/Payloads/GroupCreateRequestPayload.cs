using System.Collections.Generic;

namespace LanChat.Shared.Payloads
{
    public class GroupCreateRequestPayload
    {
        public string GroupName { get; set; } = string.Empty;
        public List<string> InitialMembers { get; set; } = new List<string>();
    }
}
