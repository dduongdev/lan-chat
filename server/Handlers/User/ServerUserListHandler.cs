using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.User
{
    public class ServerUserListHandler : IMessageHandler
    {
        private readonly SessionManager _sessionManager;

        public string RoutingKey => RoutingKeys.UserListReq;

        public ServerUserListHandler(SessionManager sessionManager)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received UserListReq from '{session.Username}'.");

            // Lấy danh sách đang online từ SessionManager
            var usernames = _sessionManager.GetOnlineUsernames().ToList();

            // Đóng gói vào Payload
            var responsePayload = new UserListResponsePayload
            {
                Usernames = usernames
            };

            // Gửi trả lại cho Client yêu cầu
            await session.SendAsync(RoutingKeys.UserListRes, responsePayload);
        }
    }
}
