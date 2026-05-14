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
                // TODO: Ở bước này, trong ứng dụng thực tế sẽ đọc `session.Username` để lưu vào AppSession.
                Console.WriteLine($"[Client UI] Đăng nhập thành công! {response.Message}");
                // TODO: Chuyển màn hình từ Login sang Dashboard/Main Chat
            }
            else
            {
                Console.WriteLine($"[Client UI] Đăng nhập thất bại: {response.Message}");
            }

            return Task.CompletedTask;
        }
    }
}
