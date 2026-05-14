using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.State;
using LanChat.Shared.Constants;

namespace LanChat.Server.Handlers.Auth
{
    /// <summary>
    /// Xử lý yêu cầu đăng xuất chủ động từ Client.
    /// Gọi phương thức dọn dẹp trung tâm của SessionManager.
    /// </summary>
    public class ServerLogoutHandler : IMessageHandler
    {
        private readonly SessionManager _sessionManager;

        public string RoutingKey => RoutingKeys.AuthLogoutReq;

        public ServerLogoutHandler(SessionManager sessionManager)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received Logout request from '{session.Username}'.");
            
            // Ủy quyền toàn bộ logic dọn dẹp cho SessionManager
            await _sessionManager.HandleDisconnectAsync(session);
        }
    }
}
