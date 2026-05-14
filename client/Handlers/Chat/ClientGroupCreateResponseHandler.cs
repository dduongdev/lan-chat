using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.Chat
{
    public class ClientGroupCreateResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.GroupCreateRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var res = payload.Deserialize<GroupCreateResponsePayload>();
            if (res != null)
            {
                if (res.Success)
                {
                    Console.WriteLine($"[Client UI] Tạo nhóm '{res.GroupName}' thành công! GroupId: {res.GroupId}");
                    // Lưu GroupId vào metadata để test các tính năng sau
                    session.SetMetadata("last_created_group_id", res.GroupId.ToString());
                }
                else
                {
                    Console.WriteLine($"[Client UI] Tạo nhóm thất bại: {res.Message}");
                }
            }
            return Task.CompletedTask;
        }
    }
}
