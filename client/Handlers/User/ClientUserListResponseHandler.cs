using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
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

            // Loại bỏ chính mình
            var others = response.Usernames.Where(u => u != session.Username).ToList();
            ChatService.Instance.RaiseUserListReceived(others);

            return Task.CompletedTask;
        }
    }
}
