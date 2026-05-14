using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Auth
{
    public class ClientRegisterResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.AuthRegisterRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var response = payload.Deserialize<RegisterResponsePayload>();
            if (response == null) return Task.CompletedTask;

            if (response.Success)
            {
                Console.WriteLine($"[Client UI] Đăng ký thành công! {response.Message}");
                // TODO: Chuyển người dùng về màn hình Login
            }
            else
            {
                Console.WriteLine($"[Client UI] Đăng ký thất bại: {response.Message}");
                // TODO: Kích hoạt lại nút Register trên UI
            }

            return Task.CompletedTask;
        }
    }
}
