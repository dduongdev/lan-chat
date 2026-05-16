using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.Entities;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Transfers
{
    /// <summary>
    /// Ngữ cảnh liên kết với một TransferToken.
    /// </summary>
    public class TransferContext
    {
        public string Action { get; set; } = string.Empty;  // "UPLOAD" hoặc "DOWNLOAD"
        public Guid FileId { get; set; }
        public string ExpectedHash { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string UploaderUsername { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// UC-06 v2: Quản lý Port 8081 (Data Plane).
    /// - TcpListener chạy độc lập trên port 8081.
    /// - Xác thực bằng Token (16 bytes đầu tiên).
    /// - Xử lý Upload: nhận stream, lưu file, kiểm tra SHA-256, cập nhật DB, trigger thông báo.
    /// - Xử lý Download: đọc file, gửi stream.
    /// </summary>
    public sealed class FileStreamManager
    {
        private readonly ConcurrentDictionary<Guid, TransferContext> _tokenStore = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly SessionManager _sessionManager;
        private TcpListener? _listener;
        private const int DataPort = 8081;
        private const int BufferSize = 81920; // 80KB chunks
        private const string UploadDirectory = "Uploads";

        public FileStreamManager(IServiceProvider serviceProvider, SessionManager sessionManager)
        {
            _serviceProvider = serviceProvider;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Đăng ký một TransferToken với ngữ cảnh tương ứng. Token hết hạn sau 5 phút.
        /// </summary>
        public void RegisterToken(Guid token, TransferContext context)
        {
            context.ExpiresAt = DateTime.UtcNow.AddMinutes(5);
            _tokenStore[token] = context;
            Console.WriteLine($"[FileStreamManager] Token {token} registered for {context.Action} (FileId={context.FileId}). Expires at {context.ExpiresAt:HH:mm:ss}");
        }

        /// <summary>
        /// Khởi chạy TcpListener trên port 8081 (non-blocking).
        /// </summary>
        public void Start()
        {
            // Đảm bảo thư mục Uploads tồn tại
            Directory.CreateDirectory(UploadDirectory);

            _listener = new TcpListener(IPAddress.Any, DataPort);
            _listener.Start();
            Console.WriteLine($"[FileStreamManager] Data Plane is listening on port {DataPort}...");

            _ = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (true)
            {
                try
                {
                    var client = await _listener!.AcceptTcpClientAsync();
                    Console.WriteLine($"[FileStreamManager] New data connection from {client.Client.RemoteEndPoint}.");
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FileStreamManager] Accept error: {ex.Message}");
                    break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();

                    // Bước 1: Đọc 16 bytes đầu tiên để lấy TransferToken
                    var tokenBytes = new byte[16];
                    int totalRead = 0;
                    while (totalRead < 16)
                    {
                        int read = await stream.ReadAsync(tokenBytes, totalRead, 16 - totalRead);
                        if (read == 0)
                        {
                            Console.WriteLine("[FileStreamManager] Connection closed before token received.");
                            return;
                        }
                        totalRead += read;
                    }

                    var token = new Guid(tokenBytes);
                    Console.WriteLine($"[FileStreamManager] Received token: {token}");

                    // Bước 2: Tra cứu Token trong MemoryCache
                    if (!_tokenStore.TryRemove(token, out var context))
                    {
                        Console.WriteLine($"[FileStreamManager] Invalid or expired token: {token}. Disconnecting.");
                        return;
                    }

                    // Kiểm tra hết hạn
                    if (DateTime.UtcNow > context.ExpiresAt)
                    {
                        Console.WriteLine($"[FileStreamManager] Token {token} has expired. Disconnecting.");
                        return;
                    }

                    Console.WriteLine($"[FileStreamManager] Token validated. Action={context.Action}, FileId={context.FileId}");

                    // Bước 3: Xử lý theo Action
                    if (context.Action == "UPLOAD")
                    {
                        await HandleUploadAsync(stream, context);
                    }
                    else if (context.Action == "DOWNLOAD")
                    {
                        await HandleDownloadAsync(stream, context);
                    }
                    else
                    {
                        Console.WriteLine($"[FileStreamManager] Unknown action: {context.Action}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStreamManager] Error handling client: {ex.Message}");
            }
        }

        /// <summary>
        /// Xử lý luồng Upload:
        /// 1. Nhận stream và ghi vào file (Uploads/{FileId}.dat)
        /// 2. Tính lại SHA-256
        /// 3. Kiểm tra toàn vẹn
        /// 4. Cập nhật DB và trigger thông báo
        /// </summary>
        private async Task HandleUploadAsync(NetworkStream networkStream, TransferContext context)
        {
            var filePath = Path.Combine(UploadDirectory, $"{context.FileId}.dat");

            try
            {
                // Ghi dữ liệu vào file
                Console.WriteLine($"[FileStreamManager] Receiving upload for FileId={context.FileId}...");
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                {
                    await networkStream.CopyToAsync(fileStream, BufferSize);
                    await fileStream.FlushAsync();
                }
                Console.WriteLine($"[FileStreamManager] Upload stream received for FileId={context.FileId}.");

                // Tính lại SHA-256
                string computedHash;
                using (var sha256 = SHA256.Create())
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                {
                    var hashBytes = await sha256.ComputeHashAsync(fileStream);
                    computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }

                Console.WriteLine($"[FileStreamManager] Hash verification: expected={context.ExpectedHash}, computed={computedHash}");

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var transfer = await dbContext.FileTransfers.FirstOrDefaultAsync(ft => ft.Id == context.FileId);

                if (transfer == null)
                {
                    Console.WriteLine($"[FileStreamManager] FileTransfer {context.FileId} not found in DB.");
                    System.IO.File.Delete(filePath);
                    return;
                }

                if (computedHash == context.ExpectedHash)
                {
                    // Hash khớp -> Cập nhật Status = "Available"
                    transfer.Status = "Available";
                    await dbContext.SaveChangesAsync();
                    Console.WriteLine($"[FileStreamManager] File {context.FileId} verified OK. Status -> Available.");

                    // Trigger thông báo: tạo Message và gửi chat.recv
                    await NotifyFileAvailableAsync(dbContext, transfer, context);
                }
                else
                {
                    // Hash không khớp -> Xóa file, cập nhật Status = "Corrupted"
                    System.IO.File.Delete(filePath);
                    transfer.Status = "Corrupted";
                    await dbContext.SaveChangesAsync();
                    Console.WriteLine($"[FileStreamManager] File {context.FileId} hash mismatch! Status -> Corrupted. File deleted.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStreamManager] Upload error for FileId={context.FileId}: {ex.Message}");

                // Dọn dẹp file nếu có lỗi
                try { System.IO.File.Delete(filePath); } catch { }

                // Cập nhật status
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var transfer = await dbContext.FileTransfers.FirstOrDefaultAsync(ft => ft.Id == context.FileId);
                    if (transfer != null)
                    {
                        transfer.Status = "Corrupted";
                        await dbContext.SaveChangesAsync();
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Tạo bản ghi Message (MessageType="File") và gửi thông báo chat.recv cho đích.
        /// </summary>
        private async Task NotifyFileAvailableAsync(AppDbContext dbContext, FileTransfer transfer, TransferContext context)
        {
            try
            {
                // Tìm uploader
                var uploader = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == transfer.UploaderId);
                if (uploader == null) return;

                Guid? receiverId = null;
                Guid? groupId = null;

                if (context.TargetType == "PRIVATE")
                {
                    var receiver = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == context.TargetId);
                    if (receiver != null) receiverId = receiver.Id;
                }
                else if (context.TargetType == "GROUP")
                {
                    if (Guid.TryParse(context.TargetId, out var gid))
                    {
                        groupId = gid;
                    }
                }

                // Tạo bản ghi Message
                var messageId = Guid.NewGuid();
                var sentAt = DateTime.UtcNow;
                var message = new Message
                {
                    Id = messageId,
                    SenderId = uploader.Id,
                    ReceiverId = receiverId,
                    GroupId = groupId,
                    Content = $"📎 {transfer.FileName}",
                    SentAt = sentAt,
                    MessageType = "File",
                    FileId = transfer.Id
                };

                dbContext.Messages.Add(message);
                await dbContext.SaveChangesAsync();

                Console.WriteLine($"[FileStreamManager] File message record created. MessageId={messageId}");

                // Tạo DTO để gửi qua chat.recv
                var messageDto = new ChatMessageDto
                {
                    ServerMessageId = messageId,
                    Sender = uploader.Username,
                    Content = $"📎 {transfer.FileName}",
                    SentAt = sentAt,
                    MessageType = "File",
                    FileId = transfer.Id,
                    FileName = transfer.FileName,
                    FileSize = transfer.FileSize,
                    FileHash = transfer.FileHash
                };

                // Gửi thông báo cho đích
                if (context.TargetType == "PRIVATE")
                {
                    // Gửi cho Receiver
                    if (_sessionManager.TryGet(context.TargetId, out var receiverSession) && receiverSession != null)
                    {
                        await receiverSession.SendAsync(RoutingKeys.ChatReceive, messageDto);
                        Console.WriteLine($"[FileStreamManager] File notification sent to '{context.TargetId}'.");
                    }

                    // Gửi cho Sender (xác nhận)
                    if (_sessionManager.TryGet(context.UploaderUsername, out var senderSession) && senderSession != null)
                    {
                        await senderSession.SendAsync(RoutingKeys.ChatReceive, messageDto);
                        Console.WriteLine($"[FileStreamManager] File notification sent to sender '{context.UploaderUsername}'.");
                    }
                }
                else if (context.TargetType == "GROUP")
                {
                    if (Guid.TryParse(context.TargetId, out var gid))
                    {
                        var members = await dbContext.GroupMembers
                            .Where(gm => gm.GroupId == gid)
                            .Include(gm => gm.User)
                            .ToListAsync();

                        int delivered = 0;
                        foreach (var member in members)
                        {
                            if (_sessionManager.TryGet(member.User.Username, out var memberSession) && memberSession != null)
                            {
                                try
                                {
                                    await memberSession.SendAsync(RoutingKeys.ChatReceive, messageDto);
                                    delivered++;
                                }
                                catch { }
                            }
                        }
                        Console.WriteLine($"[FileStreamManager] File notification broadcast to {delivered}/{members.Count} group members.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStreamManager] Error sending file notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Xử lý luồng Download: đọc file vật lý và gửi stream.
        /// </summary>
        private async Task HandleDownloadAsync(NetworkStream networkStream, TransferContext context)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var transfer = await dbContext.FileTransfers.FirstOrDefaultAsync(ft => ft.Id == context.FileId);

                if (transfer == null || transfer.Status != "Available")
                {
                    Console.WriteLine($"[FileStreamManager] Download failed: FileId={context.FileId} not available.");
                    return;
                }

                var filePath = Path.Combine(UploadDirectory, transfer.StoragePath);
                if (!System.IO.File.Exists(filePath))
                {
                    Console.WriteLine($"[FileStreamManager] Download failed: File not found on disk: {filePath}");
                    return;
                }

                Console.WriteLine($"[FileStreamManager] Sending download for FileId={context.FileId}...");
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                await fileStream.CopyToAsync(networkStream, BufferSize);
                await networkStream.FlushAsync();
                Console.WriteLine($"[FileStreamManager] Download completed for FileId={context.FileId}. {fileStream.Length} bytes sent.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStreamManager] Download error for FileId={context.FileId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Dọn dẹp các token hết hạn (được gọi bởi FileJanitorService).
        /// </summary>
        public void CleanupExpiredTokens()
        {
            var now = DateTime.UtcNow;
            var expired = _tokenStore.Where(kvp => now > kvp.Value.ExpiresAt).Select(kvp => kvp.Key).ToList();
            foreach (var token in expired)
            {
                _tokenStore.TryRemove(token, out _);
                Console.WriteLine($"[FileStreamManager] Expired token {token} removed.");
            }
        }
    }
}
