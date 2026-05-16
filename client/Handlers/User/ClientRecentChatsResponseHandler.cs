using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.User
{
    public class ClientRecentChatsResponseHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.RecentChatsRes;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var response = payload.Deserialize<RecentChatsResponsePayload>();
            if (response != null && response.Users != null)
            {
                Console.WriteLine($"[Client] Received {response.Users.Count} recent chats.");
                ChatService.Instance.RaiseRecentChatsReceived(response.Users);
            }
            return Task.CompletedTask;
        }
    }
}
