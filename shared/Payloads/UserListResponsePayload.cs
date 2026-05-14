using System.Collections.Generic;

namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload chứa danh sách những người dùng đang online.
    /// RoutingKey: user.list.res
    /// </summary>
    public class UserListResponsePayload
    {
        public List<string> Usernames { get; set; } = new List<string>();
    }
}
