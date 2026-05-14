using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupLeaveResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupLeaveRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<GroupLeaveResponsePayload>();
            if (res != null)
            {
                if (res.Success)
                    Console.WriteLine($"[Client UI] Bạn đã rời nhóm thành công.");
                else
                    Console.WriteLine($"[Client UI] Lỗi rời nhóm: {res.Message}");
            }
            return Task.CompletedTask;
        }
    }
}
