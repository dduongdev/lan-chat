using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Auth
{
    public class ClientLoginResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.AuthLoginRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var response = payload.Deserialize<LoginResponsePayload>();
            if (response == null) return Task.CompletedTask;

            if (response.Success)
            {
                // Ở bước này, trong ứng dụng thực tế sẽ đọc `session.Username` để lưu vào AppSession.
                // Ở đây, ta lấy từ Payload... wait, LoginResponsePayload doesn't have Username.
                // So we can't set it from response. But the client knows who it logged in as.
                // For this test, let's just assume we set it before or after.
                // Actually, the server didn't send Username back. We should update the Client Program to set it manually.
                Console.WriteLine($"[Client UI] Đăng nhập thành công! {response.Message}");
                // Chuyển màn hình từ Login sang Dashboard/Main Chat
            }
            else
            {
                Console.WriteLine($"[Client UI] Đăng nhập thất bại: {response.Message}");
            }

            return Task.CompletedTask;
        }
    }
}
