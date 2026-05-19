# CHƯƠNG 1: TỔNG QUAN ĐỀ TÀI VÀ PHÂN TÍCH YÊU CẦU

## 1.1 Đặt vấn đề

Trong bối cảnh công nghệ thông tin phát triển mạnh mẽ, nhu cầu trao đổi thông tin liên tục và nhanh chóng trở thành một yếu tố thiết yếu đối với cả cá nhân và tổ chức. Mặc dù các nền tảng nhắn tin qua Internet hiện nay rất phổ biến, nhưng việc sử dụng chúng trong các tổ chức, doanh nghiệp, hoặc trong môi trường cục bộ vẫn tiềm ẩn nhiều rủi ro về bảo mật thông tin, cũng như phụ thuộc hoàn toàn vào băng thông và sự ổn định của kết nối Internet bên ngoài. Do đó, việc xây dựng một ứng dụng trò chuyện và trao đổi dữ liệu hoạt động độc lập trên nền tảng mạng cục bộ (LAN) là một nhu cầu thực tiễn nhằm đảm bảo tính an toàn và tốc độ truyền tải cao nhất.

Tuy nhiên, việc thiết kế và phát triển một hệ thống giao tiếp theo thời gian thực (real-time communication) trên nền tảng mạng LAN đặt ra nhiều thách thức lớn về mặt kỹ thuật, đặc biệt là trong lĩnh vực lập trình mạng (network programming). Thách thức trọng tâm nằm ở công tác quản lý mạng đồng thời thông qua cơ chế đa tiến trình (multi-processing) và đa luồng (multi-threading). Hệ thống phải đảm bảo khả năng tiếp nhận, định tuyến và phân phát luồng dữ liệu (data streaming) liên tục từ nhiều máy khách (client) cùng lúc mà không gây ra hiện tượng nghẽn cổ chai (bottleneck) hay xung đột tài nguyên (race condition). Thêm vào đó, việc xử lý I/O mạng kết hợp với việc thiết kế cấu trúc ứng dụng sao cho mã nguồn dễ bảo trì và mở rộng cũng là một bài toán phức tạp đòi hỏi sự nghiên cứu thiết kế kiến trúc kỹ lưỡng.

## 1.2 Yêu cầu hệ thống

Dựa trên thực tiễn đánh giá và định hướng phát triển, hệ thống phần mềm cần đáp ứng đầy đủ các tiêu chuẩn kỹ thuật khắt khe, bao gồm cả yêu cầu về chức năng và yêu cầu phi chức năng.

### 1.2.1 Yêu cầu chức năng

Hệ thống phải triển khai thành công các nhóm nghiệp vụ cốt lõi sau:
1. **Quản lý danh tính người dùng:** Cho phép người dùng đăng ký tài khoản mới và đăng nhập thông qua các cơ chế xác thực an toàn.
2. **Giao tiếp văn bản (Messaging):**
   - **Chat cá nhân (Private):** Cho phép trao đổi tin nhắn trực tiếp giữa hai thực thể người dùng.
   - **Chat nhóm (Group):** Hỗ trợ khởi tạo phòng chat và trao đổi thông tin cho một nhóm từ ba người dùng trở lên.
3. **Quản lý hiện diện:** Tự động đồng bộ và hiển thị chính xác trạng thái hoạt động (online/offline) của tất cả thành viên trong hệ thống theo thời gian thực.
4. **Truyền dẫn tập tin (File Transfer):** Cung cấp cơ chế đóng gói và truyền tải an toàn các file dữ liệu đa phương tiện giữa các người dùng mà vẫn đảm bảo tính toàn vẹn của file.
5. **Giao tiếp đa phương tiện (Video Call):** Tích hợp mô-đun thiết lập kết nối peer-to-peer để truyền phát luồng dữ liệu hình ảnh (Camera) và âm thanh (Microphone) một cách ổn định.

### 1.2.2 Yêu cầu phi chức năng

Về mặt hiệu suất kiến trúc và khả năng vận hành, hệ thống phải tuân thủ các nguyên tắc thiết kế sau:
1. **Độ trễ thấp (Low Latency):** Cơ chế định tuyến và phân phối gói tin qua phương thức TCP/UDP cần được tối ưu nhằm mục tiêu giảm thiểu tối đa độ trễ, đặc biệt đối với việc stream dữ liệu thời gian thực như Video/Audio.
2. **Khả năng xử lý đa nhiệm và đồng thời (Concurrency):** Hạ tầng máy chủ (Server) phải được thiết kế để xử lý linh hoạt và duy trì bền vững hàng loạt phiên kết nối (sessions) thông qua việc quản lý pool luồng (thread pool) hoặc các mô hình tác vụ bất đồng bộ hiệu quả (asynchronous task models).
3. **Tính đóng gói và khả năng tái sử dụng (Encapsulation and Reusability):** Mã nguồn dự án cần tuân thủ triệt để các triết lý thiết kế hướng đối tượng (OOP). Cụ thể, hệ thống Core Network (xử lý byte, buffer) phải được đóng gói độc lập với Business Logic. Điều này cho phép tái sử dụng các mô-đun hạt nhân cho các dự án tích hợp mạng khác trong tương lai.