using System.Collections.Generic;

namespace LanChat.Shared.Payloads
{
    public class GroupListResponsePayload
    {
        public List<GroupInfoDto> Groups { get; set; } = new List<GroupInfoDto>();
    }
}
