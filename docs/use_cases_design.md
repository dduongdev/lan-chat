# Use Case Specification: Ứng dụng Chat LAN Đa Năng

## Tóm tắt danh sách Use Case
*   **UC-01:** Secure Handshake (Trao đổi khóa bảo mật)
*   **UC-02:** User Registration (Đăng ký tài khoản)
*   **UC-03:** User Login (Đăng nhập)
*   **UC-04:** Get Online Users (Lấy danh sách Online)
*   **UC-05:** Send Message (Chat cá nhân/Nhóm/Toàn mạng)
*   **UC-06:** File Transfer (Truyền File & Lưu lịch sử)
*   **UC-07:** Logout / Disconnect (Đăng xuất/Ngắt kết nối)
*   **UC-08:** Create Chat Group (Tạo nhóm chat)

---

## Use Case ID
UC-01

## Use Case Name
Secure Handshake (Thiết lập kênh truyền bảo mật)

## Brief Description
Hệ thống tự động thực hiện trao đổi khóa (Key Exchange) giữa Client và Server ngay khi kết nối TCP được thiết lập để mã hóa mọi gói tin sau đó.

## Actors
- Primary Actor: Client Application (Tự động)
- Secondary Actor: Server System

## Preconditions
- Client đã kết nối TCP thành công đến IP và Port của Server.

## Trigger
- Client thiết lập kết nối socket thành công.

## Main Flow
1. Client gửi gói tin yêu cầu khóa công khai (`auth.handshake.req`).
2. Server phản hồi bằng chuỗi RSA Public Key của Server.
3. Client tạo ngẫu nhiên một cặp khóa đối xứng `AES_Key` và `AES_IV`.
4. Client sử dụng RSA Public Key của Server để mã hóa `AES_Key` và `AES_IV`.
5. Client gửi gói tin chứa khóa AES đã mã hóa lên Server (`auth.handshake.res`).
6. Server dùng RSA Private Key để giải mã lấy `AES_Key` và `AES_IV`.
7. Server lưu thông tin AES vào đối tượng `TcpSession` của Client trong bộ nhớ (Memory).
8. Server gửi thông báo Handshake thành công (đã được mã hóa bằng AES).

## Alternative Flow
(Không có)

## Exception Flow
### E1. Giải mã RSA thất bại tại Server
1. Server không thể giải mã gói tin `auth.handshake.res` do sai định dạng hoặc Client dùng sai Public Key.
2. Server ghi log hệ thống cảnh báo bảo mật.
3. Server đóng kết nối TCP ngay lập tức (`Socket.Close()`).

## Postconditions
### Success
- Kênh truyền giữa Client và Server được bảo mật hoàn toàn. Mọi Payload JSON sau bước này đều bị mã hóa AES.
### Failure
- Kết nối bị ngắt, không có session nào được tạo.

## Business Rules
- Việc mã hóa AES chỉ áp dụng cho phần `Payload`, phần `RoutingKey` vẫn ở dạng plaintext để `Dispatcher` định tuyến.

## Priority
Highest

---

## Use Case ID
UC-02

## Use Case Name
User Registration

## Brief Description
Cho phép người dùng tạo tài khoản mới để tham gia hệ thống Chat. Dữ liệu được lưu vĩnh viễn vào Database.

## Actors
- Primary Actor: User

## Preconditions
- UC-01 (Secure Handshake) đã hoàn tất thành công.

## Trigger
- Người dùng điền thông tin và nhấn nút "Đăng ký".

## Main Flow
1. Người dùng nhập Username, Password và Confirm Password.
2. Client kiểm tra tính hợp lệ của dữ liệu đầu vào.
3. Client đóng gói dữ liệu và gửi gói tin `auth.register` (mã hóa AES).
4. Server giải mã, truy vấn **Database** kiểm tra xem Username đã tồn tại chưa.
5. Server Hash Password bằng thuật toán an toàn (vd: SHA-256 + Salt).
6. Server **lưu bản ghi User mới (Username, Password Hash, Thời gian tạo, Trạng thái = Offline) vào Database**.
7. Server gửi phản hồi thành công về Client.
8. Client hiển thị thông báo thành công và chuyển về màn hình Login.

## Alternative Flow
(Không có)

## Exception Flow
### E1. Username đã tồn tại
1. Server phát hiện Username đã có trong Database.
2. Server trả về gói tin lỗi (`auth.register.fail` kèm thông điệp).
3. Client hiển thị thông báo: "Tên đăng nhập đã tồn tại".

### E2. Dữ liệu không hợp lệ tại Client
1. Password và Confirm Password không khớp hoặc trống.
2. Client chặn không gửi request và thông báo lỗi cho người dùng.

## Postconditions
### Success
- Tài khoản mới được ghi nhận thành công trong Database.
### Failure
- Database không bị thay đổi.

## Business Rules
- Password không được truyền dưới dạng Plaintext (truyền qua kênh mã hóa AES).
- Username không chứa ký tự đặc biệt, tối thiểu 4 ký tự.

## Priority
High

---

## Use Case ID
UC-03

## Use Case Name
User Login

## Brief Description
Cho phép người dùng đăng nhập vào hệ thống để bắt đầu chat và đồng bộ dữ liệu.

## Actors
- Primary Actor: User

## Preconditions
- Người dùng đã có tài khoản trong Database.
- UC-01 (Secure Handshake) đã hoàn tất.

## Trigger
- Người dùng nhấn nút "Login".

## Main Flow
1. Người dùng nhập Username và Password.
2. Client gửi gói tin `auth.login` lên Server (mã hóa AES).
3. Server truy vấn **Database**, so khớp Username và Password Hash.
4. Server **cập nhật trạng thái của User thành "Online" trong Database** (ghi nhận LastLoginTime).
5. Server ánh xạ `Username` vào `SessionHandler` hiện tại và thêm vào danh sách `OnlineUsers` trong RAM (ConcurrentDictionary).
6. Server gửi gói tin `auth.login.success` về Client. *(Kèm theo lịch sử tin nhắn chưa đọc / danh sách nhóm chat từ Database).*
7. Server gửi gói tin `user.joined` (Broadcast) cho tất cả các Client khác báo hiệu có người mới online.
8. Client chuyển hướng đến màn hình Chat chính và load dữ liệu lịch sử.

## Alternative Flow
(Không có)

## Exception Flow
### E1. Sai mật khẩu hoặc Username không tồn tại
1. Server phát hiện thông tin không khớp với Database.
2. Server trả về gói tin `auth.login.fail`.
3. Client hiển thị thông báo: "Sai tên đăng nhập hoặc mật khẩu".

### E2. Tài khoản đang đăng nhập ở nơi khác
1. Server phát hiện `Username` đã có trong `OnlineUsers` (RAM).
2. Server ngắt kết nối của Session cũ (ép đăng xuất máy cũ) và lưu Session mới.
3. Server thông báo cho máy cũ bị ngắt kết nối.

## Postconditions
### Success
- Trạng thái User trên Database là Online. Session được tạo.
### Failure
- Người dùng kẹt ở màn hình Login.

## Business Rules
- Không cho phép người dùng chưa Login gửi các gói tin nghiệp vụ (`chat.*`, `file.*`, `group.*`).

## Priority
High

---

## Use Case ID
UC-04

## Use Case Name
Get Online Users

## Brief Description
Lấy và hiển thị danh sách những người dùng đang trực tuyến.

## Actors
- Primary Actor: User
- Secondary Actor: Server System

## Preconditions
- Người dùng đã Login thành công.

## Trigger
- Hệ thống Client tự động trigger sau khi Login, hoặc người dùng nhấn "Làm mới danh sách".

## Main Flow
1. Client gửi gói tin `user.list.req`.
2. Server duyệt qua `ConcurrentDictionary` (hoặc truy vấn **Database** các user có trạng thái Online).
3. Server trích xuất danh sách các Username đang hoạt động.
4. Server đóng gói vào Payload và gửi gói `user.list.res` về Client.
5. Client phân tích JSON và cập nhật giao diện (UI) danh sách online.

## Alternative Flow
### A1. Cập nhật thụ động (Passive Update)
1. Khi có người dùng khác Login (UC-03) hoặc Logout (UC-07).
2. Server tự động gửi gói tin `user.joined` hoặc `user.left` (Broadcast) đến các Client.
3. Client tự động thêm/xóa user đó khỏi giao diện.

## Exception Flow
(Không có)

## Postconditions
### Success
- Giao diện Client hiển thị chính xác danh sách trực tuyến.
### Failure
- (Không có)

## Business Rules
- Danh sách trả về chỉ chứa những người dùng có trạng thái xác thực hợp lệ.

## Priority
Medium

---

## Use Case ID
UC-05

## Use Case Name
Send Message (Chat)

## Brief Description
Gửi tin nhắn văn bản (kèm icon) đến toàn mạng, cá nhân, hoặc một nhóm chat. Nội dung được lưu trữ vĩnh viễn trên Server.

## Actors
- Primary Actor: User

## Preconditions
- Người dùng đã Login (UC-03).

## Trigger
- Người dùng gõ tin nhắn và nhấn "Send".

## Main Flow
1. Người dùng nhập nội dung, chọn đích đến (Cá nhân, Nhóm (GroupID), hoặc "ALL").
2. Client đóng gói tin vào `chat.msg` (Payload: `{To, Type, Content, Timestamp}`).
3. Client gửi gói tin lên Server.
4. Server phân tích gói tin và **lưu ngay bản ghi tin nhắn vào Database** (ID, Sender, Receiver/GroupID, Content, Timestamp).
5. Server phân tích trường `To` và định tuyến:
    - **Nếu `To == ALL`:** Server gửi gói tin đến tất cả `SessionHandler` trong hệ thống (trừ người gửi).
    - **Nếu `To == Username`:** Server tìm Session của Username đó. Nếu họ Online, gửi gói tin. (Nếu Offline, bỏ qua vì đã lưu ở DB bước 4).
    - **Nếu `To == GroupID`:** Server truy vấn **Database** lấy danh sách thành viên của Group. Server tìm Session của các thành viên đang Online và gửi gói tin đến họ.
6. Client (người nhận) hiển thị tin nhắn lên khung Chat.

## Alternative Flow
(Không có)

## Exception Flow
### E1. Lưu Database thất bại
1. Tại bước 4, Server gặp lỗi mất kết nối Database.
2. Server không gửi tin nhắn đi, trả về gói tin `chat.error` cho người gửi.
3. Client hiển thị: "Lỗi hệ thống, không thể gửi tin nhắn lúc này".

## Postconditions
### Success
- Tin nhắn được lưu vào DB và phân phối đến những người nhận đang Online.
### Failure
- Tin nhắn không được lưu, không được gửi đi.

## Business Rules
- Tin nhắn phải luôn được lưu vào Database trước khi chuyển tiếp (Routing) để đảm bảo tính toàn vẹn dữ liệu kể cả khi người nhận đang Offline.

## Priority
High

---

## Use Case ID
UC-06

## Use Case Name
File Transfer (Truyền File & Lưu Lịch Sử)

## Brief Description
Gửi file giữa các Client. Metadata và trạng thái truyền file được lưu vào Database làm lịch sử.

## Actors
- Primary Actor: Sender (Người gửi)
- Secondary Actor: Receiver (Người nhận)

## Preconditions
- Cả Sender và Receiver đều đang Online.

## Trigger
- Người gửi chọn file và nhấn "Gửi File".

## Main Flow
1. Sender chọn file. Client gửi gói tin `file.req` lên Server (chứa Tên, Kích thước, Định dạng, Người nhận).
2. Server **tạo một bản ghi FileTransfer trong Database** (Trạng thái: "Pending").
3. Server cấp một `FileTransferID` và chuyển tiếp yêu cầu tới Receiver.
4. Receiver nhận thông báo: "User X muốn gửi file Y. Chấp nhận?".
5. Receiver nhấn "Chấp nhận". Client gửi `file.accept` kèm `FileTransferID` lên Server.
6. Server **cập nhật trạng thái bản ghi trong Database thành "Transferring"**.
7. Server thông báo cho Sender bắt đầu gửi luồng Stream dữ liệu.
8. Khi truyền file hoàn tất, Sender/Receiver gửi thông báo `file.complete` lên Server.
9. Server **cập nhật trạng thái bản ghi trong Database thành "Completed"**.

## Alternative Flow
### A1. Người nhận từ chối
1. Ở Bước 4, Receiver nhấn "Từ chối".
2. Client gửi `file.reject` lên Server.
3. Server **cập nhật trạng thái trong Database thành "Rejected"** và thông báo cho Sender.

## Exception Flow
### E1. Ngắt kết nối giữa chừng
1. Đang truyền file, một bên mất mạng.
2. Server phát hiện timeout/lỗi kết nối luồng file.
3. Server **cập nhật trạng thái bản ghi trong Database thành "Failed"**.

## Postconditions
### Success
- File được lưu vào ổ cứng của Receiver. Lịch sử giao dịch được ghi nhận đầy đủ trong DB.
### Failure
- Quá trình bị hủy, DB ghi nhận trạng thái lỗi hoặc từ chối.

## Business Rules
- Nội dung file thực tế không cần lưu trong Database (để tiết kiệm dung lượng Server), chỉ lưu các thông tin Metadata (Tên, Size, Người gửi, Người nhận, Thời gian, Trạng thái).

## Priority
Medium

---

## Use Case ID
UC-07

## Use Case Name
User Logout / Disconnect

## Brief Description
Người dùng đăng xuất hoặc mất kết nối. Trạng thái trên Database được cập nhật.

## Actors
- Primary Actor: User / Mạng LAN

## Preconditions
- Người dùng đang Online.

## Trigger
- Người dùng nhấn "Logout", hoặc Socket bị ngắt do rớt mạng.

## Main Flow
1. Người dùng nhấn Logout (hoặc Server phát hiện Exception rớt mạng).
2. Server **cập nhật trạng thái của User thành "Offline" trong Database**.
3. Server xóa Session của User khỏi `OnlineUsers` (RAM).
4. Server Broadcast gói tin `user.left` cho tất cả các Client đang Online khác.
5. Server đóng `Socket.Close`.
6. Client (nếu chủ động Logout) trở về màn hình Login.

## Alternative Flow
(Không có)

## Exception Flow
(Không có)

## Postconditions
### Success
- Database ghi nhận chính xác trạng thái Offline của người dùng. Tài nguyên hệ thống được giải phóng.
### Failure
- (Không có)

## Business Rules
- Cần có cơ chế Heartbeat (Ping/Pong) định kỳ để Server phát hiện các client bị rớt mạng "im lặng" và tự động chạy luồng ngắt kết nối này.

## Priority
High

---

## Use Case ID
UC-08

## Use Case Name
Create Chat Group (Tạo nhóm chat)

## Brief Description
Cho phép một người dùng tạo một nhóm chat gồm nhiều thành viên. Nhóm được lưu trữ trong cơ sở dữ liệu.

## Actors
- Primary Actor: User (Người tạo nhóm)
- Secondary Actor: Mạng lưới Users (Thành viên được thêm)

## Preconditions
- Người dùng đang Login.
- Hệ thống có ít nhất 2 người dùng khác tồn tại trong Database.

## Trigger
- Người dùng nhấn "Tạo nhóm mới", đặt tên và chọn các thành viên.

## Main Flow
1. Người dùng nhập "Tên nhóm" và chọn danh sách các "Username" sẽ thêm vào nhóm.
2. Client gửi gói tin `group.create.req` (Payload: `{GroupName, List<Username>}`) lên Server.
3. Server xác thực dữ liệu đầu vào.
4. Server **tạo một bản ghi Nhóm mới trong Database** (sinh ra `GroupID`, `GroupName`, `Creator`).
5. Server **lưu danh sách các thành viên của nhóm vào Database** (bảng liên kết Group_Members).
6. Server gửi gói tin `group.create.res` (Thành công, kèm `GroupID`) về cho Người tạo nhóm.
7. Server lấy danh sách thành viên, kiểm tra ai đang Online thì gửi gói tin `group.invite` (Broadcast hẹp) báo cho họ biết họ vừa được thêm vào một nhóm mới.
8. Client của các thành viên nhận thông báo và tự động hiển thị nhóm mới trên giao diện.

## Alternative Flow
(Không có)

## Exception Flow
### E1. Tên nhóm không hợp lệ hoặc không đủ thành viên
1. Server kiểm tra thấy Tên nhóm trống hoặc số lượng thành viên < 3 (bao gồm người tạo).
2. Server từ chối xử lý, không lưu Database.
3. Server trả về lỗi `group.create.fail`.
4. Client hiển thị thông báo: "Nhóm phải có tên và tối thiểu 3 thành viên".

## Postconditions
### Success
- Nhóm được tạo trong Database. Mọi thành viên nhóm đều có thể bắt đầu sử dụng UC-05 để gửi tin nhắn vào `GroupID` này.
### Failure
- Không có dữ liệu được tạo.

## Business Rules
- Một nhóm bắt buộc phải có ít nhất 1 người tạo và 2 thành viên khác.
- Dữ liệu nhóm tồn tại vĩnh viễn trên Database kể cả khi tất cả thành viên Offline.

## Priority
High