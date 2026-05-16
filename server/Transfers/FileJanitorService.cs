using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Server.Data;

namespace LanChat.Server.Transfers
{
    /// <summary>
    /// UC-06 v2: Dịch vụ dọn rác chạy định kỳ.
    /// - Xoá các FileTransfer có Status == "Uploading" và CreatedAt đã quá 1 giờ.
    /// - Xoá file vật lý tương ứng.
    /// - Dọn dẹp token hết hạn.
    /// </summary>
    public static class FileJanitorService
    {
        private const string UploadDirectory = "Uploads";

        /// <summary>
        /// Chạy vòng lặp dọn rác mỗi giờ.
        /// </summary>
        public static async Task RunAsync(IServiceProvider serviceProvider, FileStreamManager fileStreamManager)
        {
            Console.WriteLine("[FileJanitor] Started. Running every hour.");
            while (true)
            {
                await Task.Delay(TimeSpan.FromHours(1));

                try
                {
                    // 1. Dọn dẹp token hết hạn
                    fileStreamManager.CleanupExpiredTokens();

                    // 2. Dọn dẹp các upload bị bỏ rơi
                    using var scope = serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var cutoff = DateTime.UtcNow.AddHours(-1);
                    var staleTransfers = await dbContext.FileTransfers
                        .Where(ft => ft.Status == "Uploading" && ft.CreatedAt < cutoff)
                        .ToListAsync();

                    if (staleTransfers.Count > 0)
                    {
                        Console.WriteLine($"[FileJanitor] Found {staleTransfers.Count} stale uploads to clean up.");

                        foreach (var transfer in staleTransfers)
                        {
                            // Xoá file vật lý
                            var filePath = Path.Combine(UploadDirectory, transfer.StoragePath);
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                                Console.WriteLine($"[FileJanitor] Deleted file: {filePath}");
                            }

                            dbContext.FileTransfers.Remove(transfer);
                        }

                        await dbContext.SaveChangesAsync();
                        Console.WriteLine($"[FileJanitor] Cleaned up {staleTransfers.Count} stale records.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FileJanitor] Error during cleanup: {ex.Message}");
                }
            }
        }
    }
}
