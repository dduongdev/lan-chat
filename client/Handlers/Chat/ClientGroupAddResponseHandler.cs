using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupAddResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupAddRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<GroupAddResponsePayload>();
            if (res != null)
            {
                if (res.Success)
                    Console.WriteLine($"[Client UI] Thêm thành viên vào nhóm thành công!");
                else
                    Console.WriteLine($"[Client UI] Lỗi thêm thành viên: {res.Message}");
            }
            return Task.CompletedTask;
        }
    }
}
