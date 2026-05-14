# Báo cáo Triển khai UC-02: User Registration

Tài liệu này tổng kết các thành phần đã được triển khai cho Use Case **User Registration**, tuân thủ nghiêm ngặt **Kiến trúc Hệ thống (System Architecture)** và **Đặc tả Thiết kế Lớp (UC-02 Class Design)**.

## 1. Tóm tắt kết quả
Chức năng đăng ký người dùng mới đã được triển khai thành công dựa trên kiến trúc **Envelope-Dispatcher-Handler**. Toàn bộ dữ liệu trao đổi (tên đăng nhập, mật khẩu) đều được gửi trên kênh truyền đã mã hóa (AES-256) thiết lập từ UC-01. Mật khẩu được bảo vệ an toàn bằng thuật toán băm `PBKDF2` (SHA256 kèm theo Salt) trước khi lưu vào SQLite Database.

---

## 2. Các thành phần đã triển khai

### 2.1. Phân hệ Dùng chung (`LanChat.Shared`)
*Lớp dữ liệu chung cho cả Client và Server.*

- **Constants/RoutingKeys.cs**: Đã cập nhật thêm 2 hằng số định tuyến cho luồng Đăng ký:
  - `AuthRegisterReq = "auth.register.req"`
  - `AuthRegisterRes = "auth.register.res"`
- **Payloads/RegisterRequestPayload.cs**: Lớp DTO nhận `Username` và `Password` từ Client.
- **Payloads/RegisterResponsePayload.cs**: Lớp DTO trả về kết quả đăng ký cho Client bao gồm cờ `Success` (bool) và thông điệp chi tiết `Message` (string).

### 2.2. Cập nhật Database (`LanChat.Server.Data`)
- Sửa đổi nhỏ trong tệp **`AppDbContext.cs`**: Cập nhật cách khai báo Check Constraints (`HasCheckConstraint`) theo chuẩn của Entity Framework Core 9 (`ToTable(t => t.HasCheckConstraint(...))`) để tránh các cảnh báo biên dịch `CS0618 Obsolete`.

### 2.3. Tầng Security (`LanChat.Server.Security`)
- **IPasswordHasher.cs**: Interface định nghĩa chức năng băm mật khẩu `HashPassword` và xác thực mật khẩu `VerifyPassword`.
- **PasswordHasher.cs**: Cài đặt (implementation) của `IPasswordHasher` dựa trên thư viện chuẩn `System.Security.Cryptography.Rfc2898DeriveBytes` (PBKDF2):
  - Kích thước Salt: 16 bytes.
  - Kích thước khóa (Hash): 32 bytes.
  - Số vòng lặp: 100,000 vòng.
  - Thuật toán băm: SHA-256.
  - Mật khẩu được mã hóa và lưu vào Entity Framework ở dạng chuỗi Base64: `Base64(Salt):Base64(Hash)`.

### 2.4. Tầng Xử lý Logic Server (`LanChat.Server.Handlers.Auth`)
- **ServerRegisterHandler.cs**: 
  - Bắt gói tin theo `RoutingKey.AuthRegisterReq`.
  - Mở rộng một **Dependency Injection Scope** độc lập trong quá trình xử lý mỗi gói tin (tránh lỗi `ObjectDisposedException` khi truy cập Database).
  - Inject thành công `AppDbContext` và `IPasswordHasher`.
  - Thực hiện kiểm tra tính hợp lệ của DTO.
  - Quét trong Database nếu `Username` đã tồn tại (So sánh không phân biệt hoa thường). Nếu có, lập tức trả về lỗi.
  - Nếu `Username` khả dụng, tiến hành băm mật khẩu, tạo một `Guid` (UUID) mới, lưu User vào EF Core DbContext và gọi `SaveChangesAsync()`.
  - Trả về gói phản hồi `RegisterResponsePayload` mang cờ `Success = true`.

### 2.5. Tầng Xử lý Giao diện Client (`LanChat.Client.Handlers.Auth`)
- **ClientRegisterResponseHandler.cs**: 
  - Đóng vai trò là Callback hứng thông điệp `AuthRegisterRes` từ máy chủ.
  - Phân tích cờ `Success`:
    - Nếu thành công: Báo thành công (hiện tại in ra Log Console, chờ tích hợp Event UI).
    - Nếu thất bại: Hiển thị thông báo (ví dụ: "Tên đăng nhập đã tồn tại") để hỗ trợ cập nhật lại UI đăng ký.

### 2.6. Tầng Tích hợp hệ thống (`Program.cs`)
- Đã đăng ký `ServiceCollection` trong `Server/Program.cs` nhằm xây dựng một Ioc Container (Dependency Injection):
  - Khai báo và cấu hình `AppDbContext` liên kết với SQLite (`Data Source=lanchat.db`).
  - Khai báo Singleton `IPasswordHasher`.
- Đã điều chỉnh các lệnh điều phối `MessageDispatcher` đăng ký Handler mới.
- Bổ sung kịch bản giả lập (Simulation) Đăng ký vào `Client/Program.cs` sau khi quá trình bắt tay AES thành công (UC-01).

---

## 3. Khẳng định tính tuân thủ Kiến trúc

1. **Separation of Concerns**: Logic quản lý DB `AppDbContext` không bị phơi bày ra lớp mạng. Việc chuyển đổi/Mapping diễn ra hoàn toàn bằng DTO ở tầng Handler.
2. **Security-First**: Luồng đăng ký **bắt buộc** phải tuân theo luồng Handshake trước đó. Nghĩa là gói đăng ký (`RegisterRequestPayload`) không thể bị nghe lén trên mạng nội bộ LAN, do đã được bọc vào chuỗi Base64 Encrypted của MessageEnvelope. Mật khẩu được bảo vệ khỏi Rainbow-tables thông qua Random Salt và PBKDF2.
3. **Dependency Injection**: Tầng Server đã chuyển dịch hoàn toàn sang kiến trúc sử dụng `IServiceProvider`. Các DbContext được phân tách Scope để xử lý đồng thời nhiều User cùng lúc mà không gây xung đột Tracker.

## 4. Kết quả quá trình Chạy thử (End-to-End Test)
Các log thu được từ màn hình Output khi chạy song song 2 Project Client/Server:

**Server Log:**
```
[Server] Received HandshakeReq. Sending Public Key...
[Server] Received HandshakeRes. Decrypting AES Key & IV...
[Server] AES Key established. Channel is now SECURE.
[Server] Received Register Request.
[Server] User 'testuser' registered successfully.
```

**Client Log:**
```
[Client] Received Server's Public Key. Generating AES Key...
[Client] Encrypting AES Key with Server's RSA Public Key and sending back...
[Client] Secure Handshake completed successfully. Channel is now encrypted with AES.
Channel is encrypted. Simulating User Registration...
[Client UI] Đăng ký thành công! Đăng ký thành công.
```

Cơ sở dữ liệu SQLite (`lanchat.db`) đã được tạo tự động và tài khoản `testuser` đã được ghi vào bảng `Users` với `PasswordHash` đúng định dạng bảo mật. Quá trình triển khai UC-02 **hoàn tất 100%**.
