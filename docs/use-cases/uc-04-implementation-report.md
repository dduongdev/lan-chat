# Báo cáo Triển khai UC-04: Get Online Users

Tài liệu này tổng kết các thành phần đã được triển khai cho Use Case **Get Online Users** (Lấy danh sách người dùng online), tuân thủ đúng yêu cầu kỹ thuật và quy tắc bảo mật từ `uc-04-class-design.md`.

## 1. Các thành phần đã triển khai

### 1.1. Phân hệ Dùng chung (`LanChat.Shared`)
- **Constants/RoutingKeys.cs**: Đã cập nhật định tuyến cho việc lấy danh sách người dùng:
  - `UserListReq = "user.list.req"`
  - `UserListRes = "user.list.res"`
- **Payloads**: 
  - Đã tạo DTO `UserListResponsePayload` có chứa thuộc tính `Usernames` (`List<string>`) nhằm trao đổi mảng tên tài khoản.
  - Sử dụng lại `UserPresencePayload` (có từ UC-03) phục vụ thông báo trực tiếp khi có tài khoản Joined/Left thay vì tạo thêm `PresencePayload` trùng lặp.

### 1.2. Phân hệ Quản lý Phiên (`LanChat.Server.State`)
- **`SessionManager.cs`**: 
  - Bổ sung phương thức `GetOnlineUsernames()`. Phương thức này truy xuất toàn bộ `Keys` trong danh sách các phiên đang duy trì (Dictionary), sau đó gọi `Sort()` để sắp xếp theo bảng chữ cái.
  - Việc sắp xếp sẵn ở Server sẽ giảm tải logic cho các Client sau này.

### 1.3. Tầng Xử lý Server (`LanChat.Server.Handlers.User`)
- **`ServerUserListHandler.cs`**:
  - Đảm nhận bắt gói tin yêu cầu lấy danh sách `user.list.req`.
  - Liên kết với Singleton `SessionManager` để gọi hàm `GetOnlineUsernames()`.
  - Trả về gói kết quả thông qua định tuyến `user.list.res` một cách độc lập cho duy nhất Client vừa đưa ra yêu cầu (không broadcast).

### 1.4. Tầng Xử lý Client (`LanChat.Client.Handlers.User`)
- **`ClientUserListResponseHandler`**:
  - Hứng kết quả `UserListRes` từ Server.
  - Xử lý mảng `Usernames` được gửi về.
  - **Self-Exclusion (Loại trừ bản thân)**: Trong lúc hiển thị hoặc binding mảng danh sách ra giao diện, Handler sử dụng biến `session.Username` để kiểm tra và chủ động bỏ qua tên tài khoản của chính Client đó. Điều này giúp giao diện không xuất hiện dòng chữ hiển thị bản thân đang "Online".

## 2. Kiểm thử End-to-End (E2E)

Hệ thống đã trải qua kịch bản kiểm thử giả lập E2E trong luồng thực thi liên kết `Handshake -> Register -> Login -> Lấy danh sách Online`.
- Sau khi Client đăng nhập thành công với tài khoản giả lập (`testuser`), Client ngay lập tức gửi gọi hàm `SendAsync(RoutingKeys.UserListReq, ...)`.
- Server nhanh chóng phản hồi `[Server] Received UserListReq from 'testuser'`.
- Client nhận được thông điệp trả về: `[Client UI] Nhận được danh sách 1 người dùng online:`.
- **Tuyệt vời nhất là**: Vì có áp dụng Self-Exclusion nên Client đã tự ẩn tên `testuser` của chính nó ra khỏi Log console (Giao diện list User bị trống hoàn toàn hợp lệ vì chỉ có đúng 1 máy trạm đang trực tuyến).

## 3. Tổng kết

Việc phát triển UC-04 diễn ra trơn tru. Bằng việc tận dụng được lớp quản lý trạng thái (`SessionManager`) từ quá trình làm UC-03, tính năng trích xuất và hiển thị danh sách người dùng Online hiện đã hoàn tất và tối ưu 100%. Luồng kiến trúc Client-Server tiếp tục vận hành ổn định mà không xảy ra bất kì tắc nghẽn luồng Thread hay thất thoát tài nguyên Database nào.

Dự án đã sẵn sàng để tiếp tục tiến sang UC-05: Private Messaging.
