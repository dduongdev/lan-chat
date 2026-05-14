using System;

namespace LanChat.Server.Entities
{
    public sealed class Message
    {
        public Guid Id { get; set; }
        public Guid SenderId { get; set; }
        public Guid? ReceiverId { get; set; }
        public Guid? GroupId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }

        public User Sender { get; set; } = null!;
        public User? Receiver { get; set; }
        public ChatGroup? Group { get; set; }
    }
}
