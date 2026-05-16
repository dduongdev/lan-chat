# Báo cáo Triển khai UC-06: File Transfer

## 1. Tóm tắt quá trình thực hiện
Tôi đã triển khai thành công Use Case Truyền File (UC-06) theo đúng thiết kế sử dụng kênh đàm phán qua Messaging và truyền file thực tế qua một kênh TCP phụ trợ do Server làm Proxy.

### Các thành phần đã triển khai:
- **`FileTransfer` Entity & Database**: Đã thêm thuộc tính `FileSize` vào entity, và cập nhật Schema (bao gồm cả nới lỏng Constraint `CK_Message_ReceiverOrGroup` để phục vụ UC-05, và thêm trạng thái `Transferring` cho UC-06).
- **`LanChat.Shared`**: Thêm 5 Payload class (`FileRequestPayload`, `FileOfferPayload`, `FileResponsePayload`, `FileStartPayload`, `FileStatusPayload`) và hằng số `RoutingKeys` tương ứng.
- **Server `FileTransferManager`**: Hoạt động như một TCP Server nội bộ. Có khả năng tự động mở Port trống (`TcpListener` port 0) để tạo kênh trung gian Proxy. Chờ 2 kết nối (Sender, Receiver) rồi đọc từng chunk byte stream (8KB) từ Sender đẩy sang Receiver.
- **Server Handlers**:
  - `ServerFileRequestHandler`: Ghi nhận trạng thái `Pending` vào SQLite và gửi `FileOffer` đến người nhận.
  - `ServerFileResponseHandler`: Nhận trả lời từ người nhận. Nếu `Accepted`, cập nhật trạng thái SQLite `Transferring`, cấp phát Port tạm thời và trả `FileStartPayload` cho cả 2 bên.
  - `ServerFileStatusHandler`: Cập nhật trạng thái `Completed` hoặc `Failed` vào SQLite.
- **Client `FileTransferClient`**: Quản lý kết nối TCP phụ trợ.
  - Sender: Mở file nguồn và truyền chunk bytes.
  - Receiver: Tạo file đích và ghi chunk bytes.
- **Client Handlers**: 
  - `ClientFileOfferHandler`: Trong bài test này được thiết lập tự động Accept (như yêu cầu demo).
  - `ClientFileStartHandler`: Xác định vai trò dựa trên Metadata, mở luồng TCP độc lập thực hiện `StartSendingAsync` hoặc `StartReceivingAsync`.
- **`SessionHandler` Metadata**: Thêm khả năng gán `SetMetadata` và `GetMetadata` để vượt rào (bypass) việc chia sẻ dữ liệu tạm thời (ví dụ: role `SENDER`/`RECEIVER`, kích thước file, đường dẫn file cần gửi) mà không làm ô nhiễm cấu trúc chung.

## 2. Kết quả kiểm thử E2E
Tôi đã tự động hóa luồng kiểm thử bằng cách gửi 1 file txt `90 bytes` thông qua 2 client `userA` và `userB`.

- **Client A** đã đọc dữ liệu từ tệp cục bộ (`testfile_userA.txt`), kết nối với Port tạm thời của Proxy Server và gửi lên 90 bytes với tiến trình 100%.
- **Client B** nhận được thư mời `FileOffer`, chấp nhận, kết nối đến cùng Proxy Port, và ghi 90 bytes thành công vào Desktop (`LanChat_Received`).
- **Server** đóng kết nối, thu hồi Port an toàn, cập nhật trạng thái `Completed` trong DB mà không có lỗi Exception rò rỉ.

Hệ thống hoạt động ổn định và các trường hợp ngắt kết nối (Disconnect) đột ngột cũng được Catch cẩn thận trong `FileTransferManager`.

## 3. Bước tiếp theo
Chuyển lên giai đoạn giao diện (UI) và thiết kế WPF/WinForms để hiển thị Message và Progress Bar cho các quá trình truyền/nhận.
