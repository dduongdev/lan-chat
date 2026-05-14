using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.File
{
    /// <summary>
    /// Xử lý cập nhật trạng thái truyền file (Completed/Failed) từ Client.
    /// </summary>
    public class ServerFileStatusHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;

        public string RoutingKey => RoutingKeys.FileStatusUpdate;

        public ServerFileStatusHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received FileStatusUpdate from '{session.Username}'.");
            try
            {
                var status = payload.Deserialize<FileStatusPayload>();
                if (status == null)
                {
                    Console.WriteLine("[Server] FileStatusUpdate payload is invalid.");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var transfer = await dbContext.FileTransfers.FirstOrDefaultAsync(ft => ft.Id == status.FileTransferId);
                if (transfer == null)
                {
                    Console.WriteLine($"[Server] FileTransfer {status.FileTransferId} not found for status update.");
                    return;
                }

                transfer.Status = status.Status;
                await dbContext.SaveChangesAsync();

                Console.WriteLine($"[Server] FileTransfer {status.FileTransferId} status updated to '{status.Status}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error processing FileStatusUpdate: {ex.Message}");
            }
        }
    }
}
