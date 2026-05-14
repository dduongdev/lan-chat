using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.User
{
    public class ClientUserListResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.UserListRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var response = payload.Deserialize<UserListResponsePayload>();
            if (response == null) return Task.CompletedTask;

            Console.WriteLine($"[Client UI] Nhận được danh sách {response.Usernames.Count} người dùng online:");
            foreach (var user in response.Usernames)
            {
                // Self-Exclusion: Loại trừ chính mình khỏi danh sách hiển thị
                if (user != session.Username)
                {
                    Console.WriteLine($"   - {user}");
                }
            }

            return Task.CompletedTask;
        }
    }
}
