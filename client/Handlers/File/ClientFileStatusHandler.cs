using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Client.Services;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Handlers.File
{
    public class ClientFileStatusHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.FileStatusUpdate;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            var status = payload.Deserialize<FileStatusPayload>();
            if (status == null) return Task.CompletedTask;

            ChatService.Instance.RaiseFileStatusReceived(status.FileTransferId, status.Status);
            return Task.CompletedTask;
        }
    }
}
