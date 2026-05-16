using System.Collections.Generic;

namespace LanChat.Shared.Payloads
{
    public class RecentChatsRequestPayload
    {
    }

    public class RecentChatDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
    }

    public class RecentChatsResponsePayload
    {
        public List<RecentChatDto> Users { get; set; } = new List<RecentChatDto>();
    }
}
