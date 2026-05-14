using System;
using System.Collections.Generic;

namespace LanChat.Server.Entities
{
    public sealed class ChatGroup
    {
        public Guid Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public Guid CreatorId { get; set; }

        public User Creator { get; set; } = null!;
        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
