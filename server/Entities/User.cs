using System;
using System.Collections.Generic;

namespace LanChat.Server.Entities
{
    public sealed class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
        public ICollection<ChatGroup> CreatedGroups { get; set; } = new List<ChatGroup>();
        public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
        public ICollection<FileTransfer> SentFileTransfers { get; set; } = new List<FileTransfer>();
        public ICollection<FileTransfer> ReceivedFileTransfers { get; set; } = new List<FileTransfer>();
    }
}
