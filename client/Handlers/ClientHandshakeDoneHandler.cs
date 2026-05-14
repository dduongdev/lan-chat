using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;

namespace LanChat.Client.Handlers
{
    public class ClientHandshakeDoneHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.HandshakeDone;

        public Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            // 1. Nhận thông báo thành công từ Server (gói tin này đã được mã hóa AES).
            // 2. Xác nhận kênh truyền đã an toàn.
            Console.WriteLine("[Client] Secure Handshake completed successfully. Channel is now encrypted with AES.");
            
            // 3. Kích hoạt giao diện người dùng chuyển sang màn hình Đăng nhập/Đăng ký.
            // (Sẽ triển khai bằng event hoặc callback trong UI layer)
            
            return Task.CompletedTask;
        }
    }
}
