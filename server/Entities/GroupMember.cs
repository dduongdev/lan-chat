using System;

namespace LanChat.Server.Entities
{
    public sealed class GroupMember
    {
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }

        public ChatGroup Group { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
