# UC11 - Video Call (Non-UI)

## 1. Phạm vi tài liệu
- Tập trung vào phần logic, message, state và networking của UC11.
- Không mô tả chi tiết UI/UX (window, button, layout).

## 2. Kiến trúc tổng quan UC11
- Control plane dùng `TCP` qua hệ thống `SessionHandler` + `MessageDispatcher` + `RoutingKeys`.
- Media plane dùng `UDP` kiểu `P2P mesh` (client gửi trực tiếp cho nhau).
- Server không relay video/audio payload; server chỉ điều phối cuộc gọi, participant và media state.

## 3. Các lớp được thêm vào cho UC11 (và mục đích)

### Shared
- `shared/Payloads/CallPayloads.cs`
Mục đích: định nghĩa toàn bộ payload control plane cho gọi video (invite, response, participant list, media state, end).
- `shared/Media/CallMediaPacket.cs`
Mục đích: định nghĩa định dạng gói UDP media thống nhất (header, codec, phân mảnh, parse/serialize).
- `shared/Constants/RoutingKeys.cs` (nhóm key `call.*`)
Mục đích: khai báo routing key UC11 cho luồng TCP.

### Server
- `server/State/CallSessionManager.cs`
Mục đích: quản lý vòng đời cuộc gọi đang active, participant, reject list, media state, giới hạn mesh.
- `server/Handlers/Call/ServerCallInviteHandler.cs`
Mục đích: xử lý yêu cầu tạo cuộc gọi và gửi invite đến người nhận.
- `server/Handlers/Call/ServerCallResponseHandler.cs`
Mục đích: xử lý accept/reject cuộc gọi và cập nhật participant.
- `server/Handlers/Call/ServerCallMediaStateHandler.cs`
Mục đích: nhận trạng thái camera/mic và broadcast cho participant khác.
- `server/Handlers/Call/ServerCallEndHandler.cs`
Mục đích: xử lý kết thúc cuộc gọi hoặc rời cuộc gọi.
- `server/Handlers/Call/ServerCallNotifier.cs`
Mục đích: helper broadcast các message call (`participant list`, `participant left`, `call ended`).

### Client
- `client/Services/CallService.cs`
Mục đích: service call cấp ứng dụng, gửi/nhận control message và điều phối `UdpMediaTransport`.
- `client/Services/UdpMediaTransport.cs`
Mục đích: mở UDP socket, quản lý peer endpoint, gửi/nhận audio-video packet, ráp fragment video.
- `client/Services/AudioCallService.cs`
Mục đích: capture/phát audio ở phía client để kết nối với `CallService`.
- `client/Services/MediaDeviceInfo.cs`
Mục đích: model thông tin thiết bị media (camera/mic).
- `client/Handlers/Call/*`
Mục đích: adapter map từng routing key `call.*` vào `CallService`.

## 4. Các lớp bị UC11 thay đổi (non-UI) và mục đích thay đổi
- `server/Program.cs`
Mục đích thay đổi:
1. Đăng ký `CallSessionManager` vào DI.
2. Register các call handlers vào `MessageDispatcher`.
3. Khi disconnect, tự động gỡ user khỏi call, broadcast `participant.left`/`call.ended`, và đồng bộ lại participant list.
- `client/Services/ChatService.cs`
Mục đích thay đổi:
1. Register các `client/Handlers/Call/*` vào dispatcher.
2. Khi clear event, gọi `CallService.Instance.ClearEvents()` để dọn state call đồng bộ với session.
- `shared/Constants/RoutingKeys.cs`
Mục đích thay đổi: thêm bộ key `call.*` để định tuyến control plane UC11.

## 5. Workflow UC11 (end-to-end)

### 5.1 Khởi tạo cuộc gọi
1. Caller gọi `CallService.StartPrivateCallAsync` hoặc `StartGroupCallAsync`.
2. Client gửi `call.invite.req` qua TCP, kèm UDP port local + trạng thái camera/mic ban đầu.
3. Server `ServerCallInviteHandler` validate target + membership + online + giới hạn participant.
4. Server tạo session call trong `CallSessionManager`.
5. Server trả `call.invite.created` cho caller (kèm `CallId`, `ParticipantId`).
6. Server gửi `call.invite.incoming` đến callee/member online.

### 5.2 Accept/Reject
1. Người nhận gửi `call.response` (`Accept=true/false`).
2. Nếu reject:
- Server ghi nhận reject.
- Nếu tất cả invited đều reject thì broadcast `call.ended`.
3. Nếu accept:
- Server thêm participant (kèm IP + UDP port) vào `CallSessionManager`.
- Nếu full mesh (vượt giới hạn) thì trả `call.ended` với reason `CallFull`.
- Nếu thành công, server broadcast `call.participant.list` cho toàn bộ participant.

### 5.3 Thiết lập UDP peer mesh
1. Client nhận `call.participant.list`.
2. `CallService` cấu hình `UdpMediaTransport` với `CallId`, `ParticipantId`.
3. `UdpMediaTransport.UpdateParticipants` lưu endpoint các peer còn lại (IP + UDP port).
4. Client gửi `Hello` packet để warm-up kết nối UDP.

### 5.4 Truyền media
1. Video:
- Client encode MJPEG, tách fragment theo `MaxPayloadSize`.
- Gửi packet type `Video` đến mọi peer trong mesh.
2. Audio:
- Client gửi PCM16 packet type `Audio` đến mọi peer.
3. Nhận media:
- Packet `Video` được ráp fragment theo `(ParticipantId, FrameId)`.
- Packet `Audio` được phát trực tiếp.

### 5.5 Đồng bộ trạng thái camera/mic
1. Client gửi `call.media.state` qua TCP khi bật/tắt camera hoặc mic.
2. Server cập nhật state trong `CallSessionManager`.
3. Server broadcast state mới cho các participant còn lại.

### 5.6 Kết thúc cuộc gọi
1. Client gửi `call.end`.
2. Server xử lý:
- Private call hoặc caller nhóm: kết thúc toàn bộ call, broadcast `call.ended`.
- Member nhóm rời call: broadcast `call.participant.left`, cập nhật list.
3. Nếu participant còn < 2 thì server tự kết thúc call với reason `NotEnoughParticipants`.
4. Nếu user disconnect đột ngột, `Program.cs` cleanup tương tự như rời call.

## 6. Giao thức UC11

### 6.1 TCP (control plane)
- Mục đích: signaling và state synchronization.
- Routing key chính:
1. `call.invite.req`
2. `call.invite.created`
3. `call.invite.incoming`
4. `call.invite.fail`
5. `call.response`
6. `call.participant.list`
7. `call.media.state`
8. `call.participant.left`
9. `call.end`
10. `call.ended`

### 6.2 UDP (media plane)
- Mục đích: truyền media realtime P2P.
- Packet format: `CallMediaPacket` (header cố định 48 bytes + payload).
- Giới hạn datagram:
1. `MaxDatagramSize = 1200`
2. `MaxPayloadSize = 1152`
- Packet type:
1. `Video`
2. `Audio`
3. `Hello`
4. `Ping`
5. `Pong`
- Codec:
1. `Mjpeg` cho video
2. `Pcm16` cho audio
- Khóa định tuyến media nội bộ:
1. `CallId` để lọc đúng cuộc gọi
2. `ParticipantId` để phân biệt nguồn gửi
3. `FrameId + FragmentIndex/FragmentCount` để ráp frame video

## 7. Ràng buộc kỹ thuật hiện tại UC11
- Giới hạn tối đa `5` participant (`MaxP2PMeshParticipants`) do dùng full mesh P2P UDP.
- Server không relay media nên chất lượng phụ thuộc kết nối trực tiếp giữa các client.
- Cơ chế fragment video có cleanup timeout ngắn để tránh giữ buffer cũ.

## 8. Tóm tắt trách nhiệm thành phần
- `Server`: signaling, authorization, participant orchestration, state broadcast, lifecycle cleanup.
- `Client CallService`: điều phối control-flow cuộc gọi.
- `Client UdpMediaTransport`: xử lý data-plane media UDP P2P.
- `Shared`: contract chung (`RoutingKeys`, payloads, UDP packet schema) để client/server tương thích.
