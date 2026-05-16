# Báo cáo Triển khai UC-03: User Login

Tài liệu này tổng kết các thành phần đã được triển khai cho Use Case **User Login** (Đăng nhập), tuân thủ đúng yêu cầu kỹ thuật và quy tắc bảo mật từ `uc-03-class-design.md`.

## 1. Các thành phần đã triển khai

### 1.1. Phân hệ Dùng chung (`LanChat.Shared`)
- **Constants/RoutingKeys.cs**: Đã cập nhật thêm các định tuyến cho Đăng nhập và Trạng thái người dùng:
  - `AuthLoginReq = "auth.login.req"`
  - `AuthLoginRes = "auth.login.res"`
  - `UserJoined = "user.joined"`
  - `UserLeft = "user.left"`
- **Payloads**: Đã tạo các cấu trúc dữ liệu giao tiếp:
  - `LoginRequestPayload`: DTO chứa `Username` và `Password`.
  - `LoginResponsePayload`: DTO chứa kết quả `Success` và `Message`.
  - `UserPresencePayload`: DTO chứa `Username` để thông báo trạng thái online/offline.

### 1.2. Phân hệ Quản lý Phiên (`LanChat.Messaging` & `LanChat.Server.State`)
- **`SessionHandler.cs` (Messaging):** Bổ sung thuộc tính `Username` để lưu trữ định danh của client trên mỗi kết nối TCP sau khi quá trình đăng nhập thành công.
- **`SessionManager.cs` (Server State):** Xây dựng dưới dạng Singleton để quản lý tập trung các kết nối của Server.
  - Bổ sung phương thức `AddOrUpdateSession`: Nâng cấp luồng đăng nhập để **đóng ngay lập tức (Disconnect)** kết nối TCP cũ nếu tài khoản đó được đăng nhập từ một thiết bị mới (Trường hợp ngoại lệ Exception Flow E2).
  - Tích hợp khả năng Broadcast `user.joined` và `user.left` tới tất cả các kết nối hợp lệ khác.

### 1.3. Tầng Xử lý Server (`LanChat.Server.Handlers.Auth`)
- **`ServerLoginHandler.cs`**:
  - Nhận yêu cầu, truy xuất Database thông qua Entity Framework Core bằng `IServiceProvider.CreateScope()`.
  - Sử dụng Singleton `IPasswordHasher` để đối chiếu mật khẩu `PBKDF2`.
  - Nếu thành công: 
    1. Gắn tên `Username` vào `SessionHandler`.
    2. Gọi `SessionManager.AddOrUpdateSession` để lưu trữ phiên và đá văng phiên trùng lặp.
    3. Gửi thông điệp xác nhận về Client.
    4. Gửi Broadcast thông báo `UserJoined` cho tất cả User đang online.

### 1.4. Tầng Xử lý Client (`LanChat.Client.Handlers`)
- Bổ sung các Handler để tiếp nhận sự kiện từ Server:
  - **`ClientLoginResponseHandler`**: Xử lý logic khi Server trả về kết quả Login (hiển thị UI thành công/thất bại).
  - **`ClientUserPresenceHandler`**: Lắng nghe chung các routing key liên quan đến trạng thái `UserJoined` và `UserLeft` nhằm cập nhật danh sách hiển thị trong tương lai.

## 2. Kiểm thử End-to-End (E2E) và Ngoại lệ E2

Để kiểm tra thiết kế, 3 kịch bản đã được chạy giả lập:
1. **Khởi chạy Server & Client 1:** Quá trình Login diễn ra suôn sẻ trên nền mã hóa AES.
2. **Khởi chạy Client 2 với cùng tài khoản:** 
   - Server ghi nhận `Login Request` từ Client 2.
   - Bắt gặp phiên của Client 1 đang hoạt động: Server lập tức đưa ra Log `[SessionManager] User 'testuser' logged in from another location. Disconnecting old session.`.
   - Kết nối TCP của Client 1 bị Server ngắt đứt (`oldSession.TcpClient.Close()`).
   - Client 1 văng ngoại lệ ngắt kết nối và thoát phiên.
   - Client 2 được Server xác thực thành công và trở thành người nắm phiên giao dịch hiện tại.
3. **Ngắt kết nối ngẫu nhiên:** Khi Client ngắt kết nối đột ngột, khối `finally` bên trong `Server.Program.cs` đã quét tự động, xóa bỏ `Username` khỏi `SessionManager` và thông báo Broadcast `UserLeft` đến toàn mạng LanChat.

## 3. Tổng kết
Tất cả các tiêu chí trong UC-03 bao gồm Main Flow (Đăng nhập thông thường), Exception Flow E2 (Đá phiên đăng nhập cũ) và Tính năng Broadcast trạng thái (UC-03.7) đã được triển khai và hoạt động trơn tru. Hệ thống đã đủ điều kiện để chuyển sang UC-04 (Lấy danh sách người dùng Online) và UC-05 (Gửi tin nhắn 1-1).
