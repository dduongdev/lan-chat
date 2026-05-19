# UC11 - Video Call: Cách Xử Lý (Handling)

## 1. Mục tiêu xử lý
- Cho phép người dùng rời group call mà không làm sập toàn bộ cuộc gọi.
- Cho phép join lại cuộc gọi nhóm đang diễn ra, thay vì tạo call mới gây lỗi.
- Hiển thị trạng thái dễ hiểu trên nút gọi để người chưa vào biết là đang có cuộc gọi.

## 2. Vấn đề trước khi sửa
- Trong group call, nếu người tạo cuộc gọi bấm `End`, server kết thúc toàn bộ call.
- Khi bấm gọi lại trong cùng group lúc call cũ vẫn còn participant, client gửi luồng tạo call mới, dễ gây xung đột trạng thái.
- Người dùng chưa vào call không có chỉ báo trực quan rõ ràng để biết group đang có cuộc gọi.

## 3. Cách xử lý đã áp dụng

### 3.1 Server: xử lý `End` trong group call
- Luật mới:
1. `PRIVATE`: ai bấm End thì kết thúc toàn bộ call.
2. `GROUP`: người bấm End chỉ rời call.
- Sau khi một người rời group call:
1. Broadcast `call.participant.left`.
2. Nếu còn < 2 participant thì broadcast `call.ended` với reason `NotEnoughParticipants` và đóng call.
3. Nếu còn đủ người thì broadcast lại `call.participant.list`.

Tệp liên quan:
- `server/Handlers/Call/ServerCallEndHandler.cs`

### 3.2 Server: join call đang diễn ra thay vì tạo call mới
- Khi nhận `call.invite.req` cho `GROUP`:
1. Kiểm tra group đó đã có active call chưa.
2. Nếu đã có:
   - Add caller vào call hiện tại.
   - Trả `call.invite.created` với `CallId` cũ + `ParticipantId` mới.
   - Broadcast participant list mới.
3. Nếu chưa có: tạo call mới như flow cũ.

Tệp liên quan:
- `server/Handlers/Call/ServerCallInviteHandler.cs`
- `server/State/CallSessionManager.cs`

### 3.3 Client: hiển thị trạng thái cuộc gọi nhóm trên nút gọi
- Client duy trì state nhóm nào đang có call.
- Khi đang ở màn hình group chat:
1. Nếu có call ongoing: nút đổi thành `Join Call`.
2. Nếu chưa có call: nút hiển thị `Start Call`.
- Khi nhận/sync event call (`incoming`, `created`, `ended`), state được cập nhật và UI refresh.

Tệp liên quan:
- `client/Services/CallService.cs`
- `client/Views/MainWindow.xaml.cs`

### 3.4 Server: đồng bộ `call.ended` cho đầy đủ đối tượng
- Trường hợp tất cả invited users reject:
1. Server gửi `call.ended` cho cả participant hiện có và invited users.
2. Sau đó mới đóng session call.

Tệp liên quan:
- `server/Handlers/Call/ServerCallResponseHandler.cs`

## 4. Workflow mới (group call)
1. A bấm gọi trong group.
2. Nếu group chưa có call: tạo call mới, gửi invite cho online members.
3. Nếu group đã có call: A join trực tiếp vào call hiện tại.
4. Một member bấm End:
- member đó rời call;
- call chỉ kết thúc khi không còn đủ participant.
5. Người ngoài call nhìn thấy nút `Join Call` và có thể vào ngay.

## 5. Ưu điểm của cách xử lý
- Đơn giản, ít thay đổi kiến trúc.
- Dễ đọc, dễ maintain: logic chủ yếu gom ở `ServerCallInviteHandler`, `ServerCallEndHandler`, `CallService`.
- Trải nghiệm thực tế tốt hơn cho nhóm > 3 người: người rời/đi vào lại không phá room call đang diễn ra.
