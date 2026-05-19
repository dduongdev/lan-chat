## CHƯƠNG 1: TỔNG QUAN ĐỀ TÀI VÀ PHÂN TÍCH YÊU CẦU

* **1.1 Đặt vấn đề** (Lý do xây dựng ứng dụng chat LAN, thách thức của lập trình mạng đa tiến trình/đa luồng).
* **1.2 Yêu cầu hệ thống**
* *Yêu cầu chức năng:* Đăng ký, đăng nhập, chat cá nhân (Private), chat nhóm (Group), quản lý trạng thái online/offline, truyền file, hội thoại video.
* *Yêu cầu phi chức năng:* Độ trễ thấp, xử lý đồng thời (Concurrency), tính đóng gói và tái sử dụng của mã nguồn.


## CHƯƠNG 2: KIẾN TRÚC TỔNG QUAN (LAYERED ARCHITECTURE)

* **2.1 Mô hình kiến trúc phân tầng** (Giải thích lý do chọn Layered Architecture để tách biệt tầng Core Network với tầng Business Logic).
* **2.2 Sơ đồ kiến trúc tổng thể** (Vẽ sơ đồ khối thể hiện sự phụ thuộc: `Ứng dụng Chat` $\rightarrow$ `Messaging Subsystem` $\rightarrow$ `Simple-TCP Subsystem`).

## CHƯƠNG 3: THIẾT KẾ VÀ TRIỂN KHAI CÁC HỆ THỐNG CON (SUBSYSTEMS)

* **3.1 Hệ thống con Simple-TCP (Tầng hạ tầng mạng)**
* *3.1.1 Mục tiêu:* Đơn giản hóa cơ chế Low-level Socket, đóng gói luồng I/O luồng dữ liệu.
* *3.1.2 Cơ chế đóng gói và truyền nhận Object:* Giải thích giao thức tuần tự hóa (Serialization/Deserialization) được sử dụng để truyền đối tượng qua Stream.
* *3.1.3 Xử lý Buffer và Đọc/Ghi dữ liệu bảo toàn (Packet Framing).*


* **3.2 Hệ thống con Messaging (Tầng điều phối tin nhắn)**
* *3.2.1 Kiến trúc Entry Point:* Điểm tiếp nhận dữ liệu tập trung từ mạng.
* *3.2.2 Cơ chế Dispatcher Pattern:* Cách hệ thống đọc header/type của Object tin nhắn và định tuyến (route) chính xác đến Controller/Handler xử lý tương ứng mà không làm nghẽn luồng chính.


## CHƯƠNG 4: THIẾT KẾ CHI TIẾT ỨNG DỤNG 

* **4.1 Biểu đồ Use Case tổng thể của ứng dụng**
* **4.2 Chi tiết các Use Case và Biểu đồ tuần tự (Sequence Diagram)**

## CHƯƠNG 5: TRIỂN KHAI CÀI ĐẶT VÀ ĐÁNH GIÁ KẾT QUẢ

* **5.1 Môi trường triển khai** (Ngôn ngữ sử dụng, thư viện, môi trường mạng LAN thử nghiệm).
* **5.2 Giao diện và kết quả chạy thử nghiệm** (Chụp màn hình các kịch bản: Chat 1-1, chat nhiều người để chứng minh đa luồng hoạt động mượt mà).
* **5.3 Đánh giá hiệu năng và các trường hợp biên** (Xử lý thế nào khi mất mạng, khi gửi dữ liệu kích thước lớn).

## KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN

* Những kết quả đã đạt được (Ưu điểm của việc tách Subsystem giúp code ứng dụng Chat cực kỳ ngắn gọn).
* Hạn chế hiện tại và hướng phát triển.