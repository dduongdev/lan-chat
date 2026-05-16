# TÀI LIỆU THIẾT KẾ: MESSAGING SUBSYSTEM (LAN CHAT)

## 1. Tổng quan
Hệ thống được thiết kế theo mô hình **Envelope-Dispatcher-Handler**. Hệ thống tách biệt hoàn toàn giữa tầng vận chuyển dữ liệu (Transport Layer) và tầng xử lý nghiệp vụ (Business Logic Layer), giúp ứng dụng dễ dàng mở rộng và bảo trì.

## 2. Tầng vận chuyển (Transport Layer - `ISimpleTcpClient`)
Đã triển khai cơ chế **Length-Prefixed Framing** để xử lý TCP Streaming:
- **Đóng gói:** Gửi 4-byte kích thước (Big-Endian) trước mỗi gói tin.
- **Giải gói:** Đọc chính xác độ dài dữ liệu cần thiết, tránh lỗi dính gói.
- **Đặc điểm:** Bất đồng bộ hoàn toàn (`Async/Await`), hỗ trợ kiểm soát kích thước gói tin tối đa (`MaxSize`).

## 3. Kiến trúc cấu trúc dữ liệu (Messaging Layer)

### 3.1. Message Envelope (Gói tin bao)
Mọi dữ liệu trao đổi giữa Client và Server đều phải đóng gói vào class này:
```csharp
public class MessageEnvelope
{
    public string RoutingKey { get; set; } = string.Empty; // Định danh hành động
    public JsonElement Payload { get; set; }               // Dữ liệu thô (Lazy-parsed)
}
```
*Việc sử dụng `JsonElement` giúp trì hoãn việc giải mã cho đến khi gói tin đến đúng nơi xử lý.*

---

## 4. Thiết kế các thành phần xử lý (Dispatcher & Handlers)

### 4.1. Interface Xử lý (IMessageHandler)
Đây là hợp đồng cho mọi tính năng (Chat, File, Auth...).
```csharp
public interface IMessageHandler
{
    string RoutingKey { get; }
    Task HandleAsync(ISimpleTcpClient client, JsonElement payload);
}
```

### 4.2. Bộ điều phối (MessageDispatcher)
Đóng vai trò là "Tổng đài", nhận tin nhắn thô từ `ISimpleTcpClient`, đọc `RoutingKey` và điều hướng đến đúng `Handler`.

```csharp
public class MessageDispatcher
{
    private readonly Dictionary<string, IMessageHandler> _handlers = new();

    public void RegisterHandler(IMessageHandler handler) 
        => _handlers[handler.RoutingKey] = handler;

    public async Task DispatchAsync(ISimpleTcpClient client, MessageEnvelope envelope)
    {
        if (_handlers.TryGetValue(envelope.RoutingKey, out var handler))
        {
            await handler.HandleAsync(client, envelope.Payload);
        }
        else
        {
            throw new Exception($"RoutingKey '{envelope.RoutingKey}' không được hỗ trợ.");
        }
    }
}
```

---

## 5. Quy trình xử lý (Workflow)

1.  **Nhận tin:** 
    - `TcpClient` lắng nghe và nhận dữ liệu qua `ReceiveObjectAsync<MessageEnvelope>()`.
2.  **Phân phối:** 
    - `Dispatcher.DispatchAsync()` được gọi với `envelope` vừa nhận.
3.  **Xử lý nghiệp vụ:**
    - `Handler` cụ thể (ví dụ: `ChatHandler`) nhận `JsonElement`.
    - `Handler` tiến hành `JsonSerializer.Deserialize<T>(payload.GetRawText())` để lấy dữ liệu thực tế.
4.  **Phản hồi (Tùy chọn):**
    - `Handler` sử dụng đối tượng `ISimpleTcpClient` được truyền vào để gửi lại phản hồi (Acknowledgement/Response).

---

## 6. Ưu điểm của thiết kế này
*   **Decoupling (Giải ghép):** `ISimpleTcpClient` không cần biết nội dung tin nhắn là gì, chỉ cần biết cách chuyển byte. `Handler` không cần biết mạng hoạt động ra sao.
*   **High Performance:** 
    *   Sử dụng `Dictionary` cho việc tìm kiếm handler (O(1)).
    *   Trì hoãn giải mã dữ liệu (`JsonElement`) giúp tiết kiệm CPU cho các gói tin không cần thiết.
*   **Scalability:** Để thêm tính năng (ví dụ: "Gửi icon"), bạn chỉ cần tạo class `IconHandler` và đăng ký vào `Dispatcher`. Không cần thay đổi mã nguồn hiện có.
*   **Security:** Dễ dàng triển khai Middleware kiểm tra xác thực (Auth) bên trong `Dispatcher` trước khi gọi `Handler.HandleAsync()`.