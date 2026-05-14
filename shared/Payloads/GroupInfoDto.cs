using System;
using System.Collections.Generic;

namespace LanChat.Shared.Payloads
{
    public class GroupInfoDto
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public List<string> Members { get; set; } = new List<string>();
    }
}
