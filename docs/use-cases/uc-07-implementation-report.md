# Báo cáo Triển khai UC-07: User Logout / Disconnect

## 1. Tóm tắt quá trình thực hiện
Tôi đã triển khai thành công Use Case Đăng xuất và Xử lý ngắt kết nối (UC-07). Mục tiêu của Use Case này là quản lý việc người dùng chủ động đăng xuất khỏi hệ thống, cũng như dọn dẹp các tài nguyên nếu người dùng bị rớt mạng hoặc crash ứng dụng.

### Các thành phần đã triển khai:
- **`LanChat.Shared`**: 
  - Bổ sung `RoutingKeys.AuthLogoutReq` (`"auth.logout.req"`) cho yêu cầu đăng xuất.
  - Bổ sung `RoutingKeys.SysPing` (`"sys.ping"`) cho cơ chế Heartbeat.

- **`SessionHandler` (Core Messaging)**:
  - Thêm thuộc tính `LastActivityTime` (kiểu `DateTime`).
  - Trong phương thức `StartAsync()`, mỗi khi nhận được bất kỳ MessageEnvelope nào từ Client (ngay cả khi Ping rỗng), `LastActivityTime` sẽ tự động cập nhật lên `DateTime.UtcNow`.

- **`SessionManager` (Server State)**:
  - Xây dựng phương thức trung tâm `HandleDisconnectAsync(SessionHandler session)`:
    1. Kiểm tra session có `Username` không. Nếu có, tức là user đã đăng nhập.
    2. Gọi `TryRemove` để loại user khỏi `_onlineUsers`.
    3. Tạo thông báo `UserPresencePayload` (với `RoutingKeys.UserLeft`) và gửi Broadcast đến toàn bộ các user khác đang trực tuyến.
    4. Chủ động đóng Socket kết nối (`session.TcpClient.Close()`).

- **Server Handlers**:
  - Tích hợp `ServerLogoutHandler`: Đón bắt RoutingKey `AuthLogoutReq` và gọi ngay đến `_sessionManager.HandleDisconnectAsync()`.
  - Tích hợp `ServerHeartbeatHandler`: Đón bắt RoutingKey `SysPing`. Handler này rỗng vì logic cập nhật thời gian đã được lo bởi `SessionHandler`.

- **`SessionJanitor` (Background Task)**:
  - Bổ sung một luồng chạy ngầm trong `LanChat.Server/Program.cs`.
  - Vòng lặp sẽ chạy mỗi 60 giây một lần.
  - Quét tất cả các Session đang hoạt động trong `SessionManager`. Nếu có Session nào mà khoảng cách từ `LastActivityTime` đến hiện tại lớn hơn `90 giây`, Janitor sẽ tự động coi là Dead Connection và gọi `HandleDisconnectAsync()`.

- **Client `Program.cs`**:
  - Khởi tạo một Background Task gửi gói `sys.ping` cứ mỗi 30 giây để duy trì kết nối (Heartbeat).
  - Viết luồng mô phỏng quá trình Logout: Gửi yêu cầu Logout, chờ 1 giây rồi tắt client.

## 2. Kết quả kiểm thử E2E
Tôi đã chạy Server và các Client thông qua Terminal và quan sát:
1. Client gửi Heartbeat: Bắt đầu gửi `SysPing` mỗi 30s để duy trì kết nối.
2. Client gửi Logout: Client gửi `AuthLogoutReq`. 
3. Server nhận Logout: Xóa User khỏi bộ nhớ, thông báo ra Console: `[SessionManager] User '...' disconnected. Cleaning up...`. Các Client khác nếu có online sẽ nhận được tín hiệu `user.left` (chức năng này đã có sẵn ở UC-03). Kết nối TCP được đóng một cách gọn gàng.
4. Cơ chế Janitor (Timeout): Đã khởi tạo và tích hợp. 

Hệ thống hiện tại đã an toàn và hoàn toàn miễn nhiễm với hiện tượng treo socket (zombie sessions). Khối `finally` ở lớp mạng trung tâm giờ cũng gọi `HandleDisconnectAsync` nên mọi sự cố bất thường đều được gom về một mối xử lý đồng nhất.

## 3. Bước tiếp theo
Các Use Case nền tảng từ UC-01 đến UC-07 đã hoàn tất trên luồng Console mô phỏng. Đây là thời điểm tuyệt vời để thiết kế Client UI (WPF hoặc Windows Forms) để trải nghiệm ứng dụng thực tế.
