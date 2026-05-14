# Báo cáo Triển khai UC-01: Secure Handshake

Tài liệu này tổng kết các thành phần đã được triển khai cho Use Case **Secure Handshake**, tuân thủ nghiêm ngặt **Kiến trúc Hệ thống (System Architecture)** và **Thiết kế Use Case (Use Case Design)**.

## 1. Tóm tắt kết quả

Luồng trao đổi khóa bảo mật (Hybrid Encryption: RSA + AES) đã được thực thi thành công. Toàn bộ các gói tin (payload) sau giai đoạn handshake sẽ được tự động mã hóa và giải mã ở tầng Framework (`LanChat.Messaging`), giúp tầng nghiệp vụ (Handlers) hoàn toàn không cần bận tâm về logic mã hóa.

---

## 2. Các thành phần đã triển khai

### 2.1. Phân hệ Dùng chung (`LanChat.Shared`)
*Lớp dữ liệu chung cho cả Client và Server.*

- **Constants/RoutingKeys.cs**: Định nghĩa các hằng số RoutingKey để tránh "magic strings" (`auth.handshake.req`, `auth.handshake.pubkey`, `auth.handshake.res`, `auth.handshake.done`).
- **Payloads/HandshakePubKeyPayload.cs**: DTO bọc Public Key (chuỗi XML) mà Server trả về.
- **Payloads/HandshakeResponsePayload.cs**: DTO bọc AES Key và IV đã mã hóa RSA do Client gửi lên.
- **Security/RsaManager.cs**: Cung cấp tiện ích sinh cặp khóa RSA 2048-bit, mã hóa (bằng Public Key) và giải mã (bằng Private Key) với padding `OAEP SHA-256`.
- **Security/AesCipher.cs**: Cung cấp tiện ích mã hóa đối xứng AES-256-CBC, hỗ trợ tự sinh khóa hoặc tái tạo đối tượng cipher từ khóa có sẵn. Mã hóa và giải mã trực tiếp từ PlainText sang Base64 string.

### 2.2. Phân hệ Định tuyến (`LanChat.Messaging`)
*Framework nhận trách nhiệm tự động hóa bảo mật.*

- **Cập nhật `IMessageHandler.cs`**: Đổi tham số từ `ISimpleTcpClient` sang `SessionHandler`. Điều này cho phép Handler tương tác với trạng thái bảo mật của phiên (cụ thể là gán `Cipher`).
- **Cập nhật `MessageDispatcher.cs`**: Điều chỉnh hàm `DispatchAsync` để phù hợp với Interface mới, truyền `SessionHandler` vào cho các handler xử lý.
- **Cập nhật `SessionHandler.cs`**: 
  - Thêm thuộc tính `Cipher` (`AesCipher`) để lưu bộ mã hóa của phiên và cờ `IsEncrypted`.
  - Mở rộng vòng lặp `StartAsync`: Tự động giải mã (decrypt) `envelope.Payload` nếu gói tin được đánh dấu là `String` (Base64) và phiên đang ở trạng thái `IsEncrypted`.
  - Thêm phương thức `SendAsync<T>`: Tự động mã hóa (encrypt) payload thành chuỗi Base64 trước khi gửi nếu kênh truyền đã an toàn.
- **Cập nhật Dependencies**: Bổ sung `ProjectReference` tới `LanChat.Shared` để lấy định nghĩa `AesCipher` (tuân thủ sơ đồ phụ thuộc: Messaging -> Shared).

### 2.3. Ứng dụng Server (`LanChat.Server`)
*Tầng xử lý nghiệp vụ của máy chủ.*

- **State/ServerSecurityState.cs**: Quản lý `RsaManager` dưới dạng Singleton để lưu giữ khóa Private Key duy nhất của Server xuyên suốt vòng đời ứng dụng.
- **Handlers/ServerHandshakeReqHandler.cs**: Bắt sự kiện `auth.handshake.req` từ Client mới kết nối, đọc Public Key từ trạng thái Singleton và phản hồi qua `HandshakePubKeyPayload`.
- **Handlers/ServerHandshakeResHandler.cs**: Bắt sự kiện `auth.handshake.res`, sử dụng RSA để giải mã AES Key/IV, khởi tạo `AesCipher` và gắn vào `session.Cipher`. Sau đó gửi thông điệp `auth.handshake.done` (thông điệp này sẽ được `SessionHandler` tự động mã hóa). Bắt lỗi `CryptographicException` nếu có hacker cố tình can thiệp.

### 2.4. Ứng dụng Client (`LanChat.Client`)
*Tầng nghiệp vụ người dùng cuối.*

- **Handlers/ClientHandshakePubKeyHandler.cs**: Bắt gói `auth.handshake.pubkey`. Sinh AES Key/IV mới ngẫu nhiên, mã hóa bằng RSA Public Key của Server, gắn `AesCipher` vào session của chính nó và gửi trả Server.
- **Handlers/ClientHandshakeDoneHandler.cs**: Lắng nghe `auth.handshake.done`. Đóng vai trò là tín hiệu để UI biết quá trình bắt tay hoàn tất và kênh truyền đã bảo mật.

---

## 3. Xác minh tính tuân thủ Kiến trúc

1. **Separation of Concerns**: Logic mã hóa RSA/AES đặt trong `Shared`. Logic định tuyến đặt trong `Messaging`. Logic xử lý thông điệp đặt trong `Server` và `Client`.
2. **Payload-Only Encryption**: Các payload được Serialize thành chuỗi, đem mã hóa rồi gắn lại vào `MessageEnvelope` dưới dạng Base64 String. `RoutingKey` luôn nằm ngoài (không mã hóa) để `MessageDispatcher` vẫn có thể định tuyến.
3. **Dependency Graph**: 
   - `LanChat.Shared` không tham chiếu project nào.
   - `LanChat.Messaging` tham chiếu `SimpleTcp` và `LanChat.Shared`.
   - `LanChat.Server` và `LanChat.Client` tham chiếu `LanChat.Messaging` và `LanChat.Shared` (nhưng không tham chiếu lẫn nhau).
   - Kiến trúc hoàn toàn không có tham chiếu vòng (Circular Dependency).

## 4. Bước tiếp theo (Next Steps)
- Khởi tạo `Program.cs` cho `Server` và `Client` để cấu hình Dependency Injection và test End-to-End luồng bắt tay này qua mạng thực tế.
- Chuyển sang triển khai các Use Case tiếp theo (Đăng nhập, Đăng ký).
