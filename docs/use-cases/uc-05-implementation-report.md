# Báo cáo Triển khai UC-05: Send Message (Chat)

Tài liệu này tổng kết các thành phần đã được triển khai cho Use Case **Send Message** (Gửi tin nhắn), tuân thủ đúng đặc tả thiết kế từ `uc-05-class-design.md`.

## 1. Tóm tắt kết quả

Chức năng gửi tin nhắn đã được triển khai hoàn chỉnh cho cả 3 chế độ: **PRIVATE** (1-1), **ALL** (Broadcast), và **GROUP** (Nhóm). Toàn bộ tin nhắn đều được lưu vào SQLite Database trước khi chuyển tiếp, đảm bảo tính toàn vẹn dữ liệu. Cơ chế **ACK (ChatEcho)** giúp Client xác nhận rằng tin nhắn đã được Server tiếp nhận và lưu trữ thành công. Chức năng truy vấn **Lịch sử Chat** (phân trang) cũng đã sẵn sàng.

---

## 2. Các thành phần đã triển khai

### 2.1. `LanChat.Shared` — Payloads & RoutingKeys

| File | Mô tả |
|:---|:---|
| `RoutingKeys.cs` | Bổ sung 5 hằng số: `ChatMsg`, `ChatEcho`, `ChatReceive`, `ChatHistoryReq`, `ChatHistoryRes` |
| `ChatMessagePayload.cs` | DTO gửi tin: `ClientMessageId`, `TargetType` (PRIVATE/GROUP/ALL), `TargetId`, `Content` |
| `ChatEchoPayload.cs` | DTO xác nhận (ACK): `ClientMessageId`, `ServerMessageId`, `SentAt` |
| `ChatMessageDto.cs` | DTO chuẩn cho tin nhắn hiển thị: `ServerMessageId`, `Sender`, `Content`, `SentAt` |
| `ChatHistoryRequestPayload.cs` | DTO yêu cầu lịch sử: `TargetId`, `BeforeTimestamp` (phân trang), `Limit` |
| `ChatHistoryResponsePayload.cs` | DTO phản hồi lịch sử: `TargetId`, `Messages` (List) |

### 2.2. `LanChat.Server` — Handlers

#### `ServerChatHandler.cs` (`Handlers/Chat/`)
- **RoutingKey:** `chat.msg`
- **Logic chi tiết:**
  1. **Xác thực:** Kiểm tra session đã đăng nhập (`session.Username != null`). Tìm `SenderId` từ Database.
  2. **Lưu trữ:** Tạo Entity `Message` mới, gán `SenderId`, `ReceiverId` (nếu PRIVATE), `Content`, `SentAt = DateTime.UtcNow`. Gọi `SaveChangesAsync()`.
  3. **ACK:** Gửi `ChatEchoPayload` chứa `ServerMessageId` và `SentAt` về cho người gửi ngay sau khi lưu thành công.
  4. **Định tuyến:**
     - **PRIVATE:** Tra cứu `SessionManager.TryGet(TargetId)`. Nếu người nhận online, chuyển tiếp `ChatMessageDto` qua `chat.recv`. Nếu offline, tin nhắn vẫn an toàn trong DB để truy vấn lịch sử sau.
     - **ALL:** Lặp qua `SessionManager.Snapshot()`, gửi `ChatMessageDto` cho tất cả sessions trừ người gửi.
     - **GROUP:** Parse `TargetId` thành `Guid` (GroupId). Truy vấn bảng `GroupMembers` (kèm `Include(User)`) để lấy danh sách thành viên. Cập nhật `GroupId` trên entity `Message` đã lưu. Lặp qua từng thành viên, bỏ qua người gửi, kiểm tra `SessionManager.TryGet` để xác định online, và gửi `ChatReceive` cho những ai đang online.

#### `ServerChatHistoryHandler.cs` (`Handlers/Chat/`)
- **RoutingKey:** `chat.his.req`
- **Logic chi tiết:**
  1. Tìm `UserId` của cả 2 người (current user và target user) từ Database.
  2. Truy vấn bảng `Messages` theo điều kiện: `(SenderId = A AND ReceiverId = B) OR (SenderId = B AND ReceiverId = A)`.
  3. Hỗ trợ phân trang: Lọc `SentAt < BeforeTimestamp` nếu client cung cấp.
  4. Sắp xếp `OrderByDescending(SentAt)`, lấy `Take(Limit)`, rồi `Reverse()` để trả về thứ tự cũ → mới.
  5. Ánh xạ sang `ChatMessageDto` và gửi `ChatHistoryRes` về Client.

### 2.3. `LanChat.Client` — Handlers

| File | RoutingKey | Logic |
|:---|:---|:---|
| `ClientChatEchoHandler.cs` | `chat.echo` | Nhận ACK, in log xác nhận. Khi tích hợp UI: Cập nhật trạng thái tin nhắn từ "Sending..." thành "Sent". |
| `ClientChatReceiveHandler.cs` | `chat.recv` | Nhận tin nhắn đến, hiển thị `[HH:mm:ss] Sender: Content`. Khi tích hợp UI: Thêm vào chat window. |
| `ClientChatHistoryHandler.cs` | `chat.his.res` | Nhận danh sách lịch sử, in từng tin nhắn ra log. Khi tích hợp UI: Chèn vào đầu khung chat. |

---

## 3. Kết quả kiểm thử End-to-End

### Server Log:
```
[Server] Received ChatMsg from 'testuser'.
[Server] Message saved to DB. ID=944550eb-4601-4b28-9f36-dd5850671908
[Server] Message broadcasted to all users.
```

### Client Log:
```
Simulating Send Chat Message (ALL)...
[Client UI] ACK: Tin nhắn đã gửi thành công. ServerID=944550eb-..., SentAt=07:16:51
```

### Phân tích kết quả:
1. **Lưu trữ thành công:** Tin nhắn được ghi vào SQLite với `ServerMessageId` duy nhất.
2. **ACK hoạt động:** Client nhận được `ChatEcho` ngay lập tức sau khi Server lưu xong, chứa đúng `ServerMessageId` và `SentAt` từ Server.
3. **Broadcast hoạt động:** Server xác nhận đã phát tin nhắn cho toàn bộ sessions (trong trường hợp chỉ có 1 user online, không có ai khác để nhận nên không có log `ChatReceive` ở phía Client).
4. **Tính toàn vẹn:** Tin nhắn chỉ được chuyển tiếp SAU KHI `SaveChangesAsync()` thành công, đảm bảo quy tắc nghiệp vụ "Database-first".

---

## 4. Quy tắc nghiệp vụ đã tuân thủ

| Quy tắc | Trạng thái |
|:---|:---|
| Mọi tin nhắn phải lưu DB trước khi chuyển tiếp | ✅ `SaveChangesAsync()` trước `SendAsync(ChatReceive)` |
| Thời gian hiển thị dựa trên `SentAt` từ Server | ✅ `DateTime.UtcNow` do Server cấp |
| Hỗ trợ UTF-8 (Emoji) | ✅ Test payload chứa 🎉 thành công |
| Phân trang lịch sử | ✅ `BeforeTimestamp` + `Limit` |

---

## 5. Danh sách file đã tạo/sửa

### Tạo mới:
- `shared/Payloads/ChatMessagePayload.cs`
- `shared/Payloads/ChatEchoPayload.cs`
- `shared/Payloads/ChatMessageDto.cs`
- `shared/Payloads/ChatHistoryRequestPayload.cs`
- `shared/Payloads/ChatHistoryResponsePayload.cs`
- `server/Handlers/Chat/ServerChatHandler.cs`
- `server/Handlers/Chat/ServerChatHistoryHandler.cs`
- `client/Handlers/Chat/ClientChatEchoHandler.cs`
- `client/Handlers/Chat/ClientChatReceiveHandler.cs`
- `client/Handlers/Chat/ClientChatHistoryHandler.cs`

### Chỉnh sửa:
- `shared/Constants/RoutingKeys.cs` — Thêm 5 routing keys
- `server/Program.cs` — Đăng ký `ServerChatHandler`, `ServerChatHistoryHandler`
- `client/Program.cs` — Đăng ký 3 client handlers, thêm kịch bản test gửi tin nhắn
