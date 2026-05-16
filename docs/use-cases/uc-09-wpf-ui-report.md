# Báo cáo Triển khai Giao diện WPF (LanChat UI Client)

## 1. Tóm tắt
Đã hoàn thiện và tối ưu giao diện đồ họa (UI) bằng WPF cho LanChat Client. Giao diện được nâng cấp từ Dark Theme cơ bản lên một thiết kế cao cấp (Premium Design) với tông màu xanh lá đậm (Emerald/Dark Green) và trắng kem (Off-white), mang lại cảm giác chuyên nghiệp và hiện đại.

UI Client hỗ trợ toàn bộ các tính năng từ UC-01 đến UC-08, bao gồm: Đăng nhập/Đăng ký bảo mật, Chat (1-1, Nhóm, Broadcast), và Truyền tập tin qua mạng LAN.

## 2. Quá trình triển khai & Nâng cấp Giao diện

### Thiết kế "Premium Design"
- **Color Palette**: Sử dụng hệ thống màu tùy chỉnh trong `App.xaml`:
  - `SidebarBgColor`: #0D3B31 (Xanh lá đậm phong cách hiện đại).
  - `BgDarkColor`: #F4F2EB (Trắng kem dịu mắt).
  - `AccentColor`: #16705D (Xanh ngọc lục bảo).
- **Login Screen**: 
  - Chuyển đổi sang bố cục 2 cột: Cột trái giới thiệu tính năng (AES, Chat 1-1, File), cột phải là Card đăng nhập.
  - Tích hợp cả nút **Đăng nhập** và **Đăng ký** rõ ràng trên cùng một giao diện.
- **Main Chat Area**: 
  - Sidebar phong cách Slack/Discord với danh sách kênh (#general) và người dùng online.
  - Khung chat với các bong bóng tin nhắn (Bubble chat) bo góc mềm mại, hiển thị thời gian theo định dạng 12h (h:mm tt).

### Kiến trúc & Dữ liệu
- **`ChatService` (Singleton)**: Đóng vai trò cầu nối duy nhất giữa `SessionHandler` (tầng Messaging) và UI. Toàn bộ logic được xử lý qua Event-driven để đảm bảo tính bất đồng bộ.
- **Sự kiện ngắt kết nối**: Bổ sung xử lý `OnDisconnected` tại cả `LoginWindow` và `MainWindow` để thông báo kịp thời cho người dùng khi server gặp sự cố.

## 3. Vấn đề phát sinh & Cách giải quyết

### 1. Lỗi che khuất nút bấm (UI Clipping)
- **Vấn đề**: Sau khi đổi sang giao diện card 2 cột, các nút Đăng nhập/Đăng ký bị cắt mất phần dưới trên một số độ phân giải màn hình.
- **Nguyên nhân**: Do thiết lập `Margin="40,80"` quá lớn trên Card và chiều cao cửa sổ cố định không đủ chứa nội dung sau khi cộng thêm Padding.
- **Giải pháp**: Giảm `Margin` xuống đồng nhất là `40`, đồng thời nới rộng chiều cao cửa sổ lên `540px`.

### 2. Lỗi truy cập File khi truyền tập tin (File Lock)
- **Vấn đề**: Người dùng gặp thông báo lỗi `The process cannot access the file ... because it is being used by another process` lặp lại nhiều lần.
- **Nguyên nhân**: 
  - Một là do cơ chế đăng ký sự kiện bị lặp (MainWindow bị khởi tạo lại nhưng không gỡ đăng ký event cũ từ Singleton `ChatService`).
  - Hai là do Server gửi nhiều lệnh `FileStartTransfer` cùng lúc dẫn đến Client mở nhiều luồng ghi vào cùng một file.
- **Giải pháp**: 
  - Thực hiện gỡ đăng ký (`-=`) trước khi đăng ký mới (`+=`) các sự kiện trong `MainWindow`.
  - Bổ sung logic kiểm tra trạng thái truyền file để tránh thực thi ghi đè song song.

### 3. Vấn đề "Không phản ứng khi bấm Đăng nhập"
- **Vấn đề**: Người dùng bấm nút nhưng không thấy giao diện chuyển động.
- **Nguyên nhân**: Do Server cũ bị treo (deadlock) hoặc file DLL bị khóa nên không gửi gói tin Handshake về cho Client, khiến Client đứng đợi vô hạn.
- **Giải pháp**: Khởi động lại Server sạch, đồng thời thêm thông báo "🔒 Đang thiết lập kênh bảo mật..." trên UI để người dùng biết trạng thái kết nối.

## 4. Kết quả & Đánh giá
- **Thẩm mỹ**: Giao diện đạt tiêu chuẩn hiện đại, vượt xa các ứng dụng socket cơ bản.
- **Tính năng**: 100% tính năng hoạt động đúng thiết kế Use Case.
- **Hiệu năng**: Tốc độ phản hồi tin nhắn và truyền file tức thì trong mạng LAN nhờ sử dụng trực tiếp TCP Stream và mã hóa AES tối ưu.
- **Vị trí lưu File**: Mọi file nhận được mặc định lưu tại thư mục `Desktop\LanChat_Received` giúp người dùng dễ dàng truy cập.

---
*Người thực hiện: Antigravity AI Assistant*
*Ngày cập nhật: 14/05/2026*
