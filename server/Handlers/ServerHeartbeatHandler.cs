using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;

namespace LanChat.Server.Handlers
{
    /// <summary>
    /// Xử lý ping heartbeat từ client. 
    /// LastActivityTime đã được cập nhật ở tầng SessionHandler mỗi khi nhận bất kỳ gói tin nào,
    /// nên handler này thực chất có thể không cần logic gì thêm, chỉ để tránh lỗi Unhandled RoutingKey.
    /// </summary>
    public class ServerHeartbeatHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.SysPing;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            // Do nothing. LastActivityTime is already updated by SessionHandler.StartAsync
            return Task.CompletedTask;
        }
    }
}
