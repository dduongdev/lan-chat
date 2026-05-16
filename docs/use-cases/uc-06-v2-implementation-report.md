# Tài liệu Triển khai UC6 v2: Store & Forward File Transfer

## 1. Quá trình Triển khai
Việc triển khai UC-06 v2 được chia làm các bước rõ ràng bám sát theo tài liệu thiết kế:

### 1.1 Cập nhật Shared Payloads & Routing Keys
- Đã loại bỏ các Routing Keys cũ (`file.req`, `file.offer`, v.v.) và thay thế bằng các key mới: `file.upload.req`, `file.download.req`, `file.transfer.res`.
- Tạo mới các lớp DTO để phục vụ luồng yêu cầu có tích hợp `TransferToken` và băm SHA-256 (`FileUploadRequestPayload`, `FileDownloadRequestPayload`, `TransferTokenResponsePayload`).
- Cập nhật `ChatMessageDto` để hỗ trợ hiển thị tin nhắn chứa thông báo file đính kèm với `MessageType = "File"`.

### 1.2 Nâng cấp Database Schema
- Cập nhật thực thể `FileTransfer` loại bỏ `SenderId`/`ReceiverId` và chuyển sang `UploaderId`, `StoragePath`, `FileHash`, `TargetType`, và `TargetId` để dễ dàng hỗ trợ gửi file cho cả cá nhân (PRIVATE) và nhóm (GROUP).
- Thêm thuộc tính `MessageType` và `FileId` vào thực thể `Message` để lưu trữ tin nhắn chứa tệp tin đính kèm vào lịch sử chat.
- Ràng buộc trạng thái tệp tin (Status) thành `Uploading`, `Available`, `Corrupted`.

### 1.3 Triển khai Control Plane (Port 8080)
- Tạo `ServerFileUploadHandler` và `ServerFileDownloadHandler` để kiểm tra quyền hạn, tạo bản ghi DB, sinh `TransferToken` (Guid 16-byte) và đăng ký với Data Plane qua bộ đệm.
- Token chỉ có thời hạn 5 phút.
- **Xác thực quyền tải xuống**: Đã áp dụng logic kiểm soát chặt chẽ ở `ServerFileDownloadHandler`. Người dùng chỉ được cấp Token Download nếu thỏa mãn một trong các điều kiện:
  1. Là người tải tệp lên ban đầu (Uploader).
  2. Là người nhận đích (TargetId) đối với tệp tin dạng PRIVATE.
  3. Là thành viên của nhóm đích (Group Member) đối với tệp tin dạng GROUP.

### 1.4 Triển khai Data Plane (Port 8081) - `FileStreamManager`
- Chạy một `TcpListener` độc lập ở cổng 8081.
- Data Plane không cần giải mã AES, cho phép luồng Stream được đọc/ghi trực tiếp (raw network stream), tối ưu hoá tốc độ (throughput).
- **Quy trình xác thực**: Đọc 16 bytes đầu tiên từ Client, đối chiếu với Token trong bộ đệm. Nếu khớp, Token sẽ bị xóa ngay lập tức (Chống Replay Attack) và kết nối được cấp quyền Upload/Download.
- **Upload**: Nhận file, lưu tạm vào thư mục `Uploads`, tính toán mã SHA-256 nội dung sau khi tải. Nếu khớp với Hash Client gửi, trạng thái file chuyển sang `Available` và kích hoạt thông báo (qua `chat.recv`). Nếu sai, xóa file lỗi và cập nhật thành `Corrupted`.
- **Download**: Nhận Token, truyền tải file từ server xuống client. Client sau khi nhận sẽ tự xác minh lại Hash để đảm bảo toàn vẹn End-to-End.

### 1.5 Triển khai Client UI và Background Services
- **Client Handlers**: Cập nhật `ChatService` và UI (WPF) để tuân thủ luồng tải lên và tải xuống.
- **FileJanitorService**: Service chạy ngầm trên Server quét mỗi giờ để dọn dẹp các tệp tin treo (Uploading quá lâu) hoặc hết hạn, đồng thời giải phóng Token.

## 2. Các Vấn đề Gặp phải và Giải pháp

### Vấn đề 1: Trùng lặp/Dư thừa các Event Truyền File cũ
**Vấn đề**: WPF Code-behind của `MainWindow` sử dụng các event cũ như `OnFileOfferReceived` vốn được thiết kế cho luồng Proxy P2P và hỏi "Bạn có muốn nhận không?". Với mô hình Store & Forward, việc hỏi nhận file là không cần thiết (file được đính kèm vào hộp thoại chat và người dùng click để tải về).
**Giải pháp**: Xóa bỏ các handler cũ, refactor toàn bộ `ChatService` để thay thế bằng các event mới: `OnFileUploadStarted`, `OnFileDownloadCompleted`, `OnFileTransferError` và bổ sung hiển thị tin nhắn hệ thống trên UI.

### Vấn đề 2: Lỗi Build do Project References & Target Frameworks
**Vấn đề**: Dự án test console (`test-console`) gặp khó khăn khi tham chiếu tới các lớp tiện ích (handlers/transfers) bên trong project `LanChat.Client` vì Client là WPF Project (`net9.0-windows`) trong khi TestConsole là bản Console thông thường (`net9.0`). Điều này gây ra lỗi thiếu tham chiếu không tương thích.
**Giải pháp**: Đồng bộ `TargetFramework` của dự án `test-console` thành `<TargetFramework>net9.0-windows</TargetFramework>` để có thể tái sử dụng chung toàn bộ logic Client mà không bị lỗi phiên bản thư viện.

### Vấn đề 3: Đảm bảo Data Plane Không Chặn Luồng (Non-blocking)
**Vấn đề**: FileTransferManager sử dụng vòng lặp While true đọc NetworkStream. Nếu thao tác này chạy chung với Thread chính hoặc vòng lặp của Control Plane, nó sẽ gây nghẽn.
**Giải pháp**: Hàm `AcceptLoopAsync` và `HandleClientAsync` của `FileStreamManager` được gói gọn trong các `Task.Run` độc lập, tránh làm đứng Server khi các file lớn đang được stream.

## 3. Kết quả Kiểm chứng (Testing Results)
Đã sử dụng Script Test để mô phỏng một Client tải lên tệp tin và kiểm tra luồng Store & Forward:
- Tệp tin mẫu `test_upload.txt` kích thước 4.8KB được tạo và tạo mã băm thành công (`c9c174387565042be573416b96297cbc44f660224f6ee3e4cf6588da3a19cf74`).
- Cổng 8080 (Control Plane) phản hồi `TransferToken` (ID: 5fa04a3f...).
- Client ngay lập tức mở kết nối phụ vào cổng 8081 (Data Plane) và gửi 16 byte Token theo sau là luồng Stream file.
- `FileStreamManager` của Server nhận, xác thực Token, lưu file và kiểm tra băm SHA-256 hoàn tất (Hash verification OK). Trạng thái chuyển sang `Available`.
- Hệ thống kích hoạt thông báo (Message record created) và gửi gói tin nhắn `chat.recv` chứa ID của tệp tin cho cả người gửi (A) và người nhận (B).
- **Kết luận**: Tính năng truyền tệp qua luồng Data Plane độc lập và xác minh End-to-End đã hoạt động xuất sắc đúng như tài liệu.

## 4. Những Điểm Khác Biệt và Mở Rộng So Với Thiết Kế
Trong quá trình triển khai, một số chi tiết đã được mở rộng và điều chỉnh so với bản vẽ ban đầu để hệ thống hoàn thiện hơn:

1. **Hỗ trợ Gửi File cho Nhóm (GROUP)**: Tài liệu tập trung vào truyền tệp 1-1. Hệ thống đã được chủ động nâng cấp với `TargetType` và `TargetId` để cho phép đính kèm tệp tin vào cả nhóm chat.
2. **Quản lý Token trong Bộ nhớ**: Thay vì dùng Distributed Cache, `ConcurrentDictionary<Guid, TransferContext>` được tích hợp trực tiếp vào `FileStreamManager` để lưu ngữ cảnh xác thực (bao gồm Action, FileId, Hash).
3. **Luồng Thông Báo Liền Mạch (chat.recv)**: Việc thông báo file mới được nhúng thẳng vào luồng tin nhắn thông thường. Bản ghi `Message` được tạo với `MessageType = "File"`, giúp file hiển thị như một tin nhắn trò chuyện liền mạch cho cả Sender và Receiver.
