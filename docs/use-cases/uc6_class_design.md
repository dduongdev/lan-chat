# UC-06: File Transfer - Đặc tả Thiết kế Lớp

## 1. Tổng quan Kiến trúc
Luồng truyền file được chia thành 2 giai đoạn riêng biệt:
1.  **Giai đoạn Đàm phán (Negotiation Phase):** Sử dụng hệ thống `LanChat.Messaging` hiện có để trao đổi metadata (tên file, kích thước) và xin phép người nhận.
2.  **Giai đoạn Truyền dữ liệu (Data Transfer Phase):** Sau khi đàm phán thành công, Server sẽ mở một kênh TCP/IP tạm thời. Cả Sender và Receiver sẽ kết nối vào kênh này để Server làm trung gian (Proxy) chuyển tiếp dòng byte (stream) của file.

**Quyết định thiết kế:** Luồng dữ liệu file **TUYỆT ĐỐI KHÔNG** được gửi qua `MessageEnvelope` để tránh làm tắc nghẽn luồng xử lý tin nhắn chung.

---

## 2. Thiết kế Payload (`LanChat.Shared`)

### 2.1. Hằng số Routing Keys
```csharp
public static partial class RoutingKeys
{
    // Negotiation
    public const string FileRequest       = "file.req";       // Sender -> Server
    public const string FileOffer         = "file.offer";     // Server -> Receiver
    public const string FileResponse      = "file.res";       // Receiver -> Server
    public const string FileStartTransfer = "file.start";     // Server -> Sender & Receiver

    // Status Update
    public const string FileStatusUpdate  = "file.status";    // Client -> Server (Báo hoàn tất/lỗi)
}
```

### 2.2. DTOs (Data Transfer Objects)

**A. `FileRequestPayload` (Yêu cầu gửi file)**
- **Ai gửi:** Sender
- **RoutingKey:** `file.req`
| Thuộc tính | Kiểu | Mô tả |
| :--- | :--- | :--- |
| `ReceiverUsername`| `string` | Tên người nhận. |
| `FileName` | `string` | Tên file và phần mở rộng. |
| `FileSize` | `long` | Kích thước file (bytes). |

**B. `FileOfferPayload` (Lời mời nhận file)**
- **Ai gửi:** Server
- **RoutingKey:** `file.offer`
| Thuộc tính | Kiểu | Mô tả |
| :--- | :--- | :--- |
| `FileTransferId`| `Guid` | ID duy nhất của phiên truyền file (lấy từ DB). |
| `SenderUsername`| `string` | Tên người gửi. |
| `FileName` | `string` | Tên file. |
| `FileSize` | `long` | Kích thước file (bytes). |

**C. `FileResponsePayload` (Phản hồi lời mời)**
- **Ai gửi:** Receiver
- **RoutingKey:** `file.res`
| Thuộc tính | Kiểu | Mô tả |
| :--- | :--- | :--- |
| `FileTransferId`| `Guid` | ID của phiên truyền file cần phản hồi. |
| `Accepted` | `bool` | `true` nếu đồng ý, `false` nếu từ chối. |

**D. `FileStartPayload` (Bắt đầu truyền file)**
- **Ai gửi:** Server (gửi cho cả hai)
- **RoutingKey:** `file.start`
| Thuộc tính | Kiểu | Mô tả |
| :--- | :--- | :--- |
| `FileTransferId`| `Guid` | ID của phiên truyền file. |
| `TransferHost` | `string` | Địa chỉ IP của Server. |
| `TransferPort` | `int` | Port tạm thời Server đã mở cho phiên này. |

---

## 3. Thiết kế tầng Server (`LanChat.Server`)

### 3.1. Lớp quản lý `FileTransferManager` (Singleton)
- **Vai trò:** Chịu trách nhiệm quản lý các kênh TCP/IP tạm thời.
- **Nơi đặt:** `LanChat.Server/Transfers/FileTransferManager.cs`

| Phương thức | Vai trò | Logic |
| :--- | :--- | :--- |
| `InitiateTransfer(...)`| Tạo kênh truyền mới. | 1. Mở một `TcpListener` trên một Port ngẫu nhiên còn trống. <br> 2. Lưu `FileTransferId` và `TcpListener` vào một `ConcurrentDictionary`. <br> 3. Trả về `Port` đã mở. <br> 4. Chạy một `Task` nền để `Accept` 2 kết nối (từ Sender và Receiver) và thực hiện sao chép stream. |

### 3.2. Handlers

**A. `ServerFileRequestHandler` (`file.req`)**
1.  Nhận `FileRequestPayload`.
2.  **Tạo bản ghi trong DB:** Tạo một Entity `FileTransfer` với trạng thái `"Pending"`.
3.  Lấy `FileTransferId` (Guid) vừa được DB sinh ra.
4.  Gửi gói tin `FileOfferPayload` đến Receiver.

**B. `ServerFileResponseHandler` (`file.res`)**
1.  Nhận `FileResponsePayload`.
2.  **Cập nhật DB:**
    - Nếu `Accepted == true`: Cập nhật trạng thái thành `"Transferring"`.
    - Nếu `Accepted == false`: Cập nhật trạng thái thành `"Rejected"`, đồng thời gửi thông báo từ chối về cho Sender và kết thúc luồng.
3.  **Khởi tạo kênh truyền (nếu Accepted):**
    - Gọi `FileTransferManager.InitiateTransfer()` để lấy Port.
    - Tạo `FileStartPayload` (chứa IP Server và Port vừa tạo).
    - Gửi gói tin này đồng thời cho cả **Sender** và **Receiver**.

---

## 4. Thiết kế tầng Client (`LanChat.Client`)

### 4.1. Lớp quản lý `FileTransferClient`
- **Vai trò:** Đóng gói logic truyền/nhận file của Client.
- **Nơi đặt:** `LanChat.Client/Transfers/FileTransferClient.cs`
- **Logic:**
  - **`StartSending(host, port, filePath)`:**
    1.  Tạo một `TcpClient` mới, kết nối đến `host:port` của Server.
    2.  Mở `FileStream` để đọc file.
    3.  Đọc từng khối (chunk) byte từ `FileStream` và ghi vào `NetworkStream` của `TcpClient`.
    4.  Báo cáo tiến độ lên UI.
    5.  Khi hoàn tất/lỗi, gửi gói `file.status` lên Server.
  - **`StartReceiving(host, port, savePath)`:** Logic tương tự nhưng ngược lại (đọc từ `NetworkStream`, ghi vào `FileStream`).

### 4.2. Handlers

**A. `ClientFileOfferHandler` (`file.offer`)**
1.  Nhận `FileOfferPayload`.
2.  **Hiển thị UI:** Mở một `MessageBox` hoặc `Popup` hỏi người dùng: "User X muốn gửi file Y (Z MB). Đồng ý?".
3.  Dựa trên lựa chọn của người dùng, gửi lại gói `FileResponsePayload` lên Server.

**B. `ClientFileStartHandler` (`file.start`)**
1.  Nhận `FileStartPayload`.
2.  Xác định vai trò (mình là người gửi hay người nhận) dựa trên `FileTransferId`.
3.  Gọi `FileTransferClient.StartSending()` hoặc `StartReceiving()` tương ứng với các tham số `host` và `port` nhận được.

---

## 5. Sơ đồ Tuần tự (Sequence Diagram)
Sơ đồ mô tả sự phối hợp giữa luồng Điều khiển (Messaging) và luồng Dữ liệu (TCP Stream).

```mermaid
sequenceDiagram
    participant Sender
    participant Server
    participant Receiver

    box "Giai đoạn 1: Đàm phán (Qua kênh Messaging)"
        Sender->>Server: [file.req] { receiver: "B", file: "a.zip" }
        Server->>Server: Tạo bản ghi DB (Status: Pending)
        Server->>Receiver: [file.offer] { sender: "A", transferId: "xyz" }
        
        Receiver->>Server: [file.res] { transferId: "xyz", accepted: true }
        Server->>Server: Cập nhật DB (Status: Transferring)
        
        Server->>Server: Mở Port tạm thời (vd: 5001)
        Server-->>Sender: [file.start] { host: "server_ip", port: 5001 }
        Server-->>Receiver: [file.start] { host: "server_ip", port: 5001 }
    end

    box "Giai đoạn 2: Truyền dữ liệu (Qua kênh TCP Stream riêng)"
        Sender-)+Server: Kết nối TCP đến port 5001
        Receiver-)+Server: Kết nối TCP đến port 5001
        
        Note over Server: Server nhận 2 kết nối,<br/>bắt đầu proxy stream
        
        Sender->>Server: Gửi [Dữ liệu file...]
        Server->>Receiver: Chuyển tiếp [Dữ liệu file...]
        
        Server-)-Sender: Đóng kết nối
        Server-)-Receiver: Đóng kết nối
    end
    
    box "Giai đoạn 3: Cập nhật trạng thái (Qua kênh Messaging)"
        Sender->>Server: [file.status] { transferId: "xyz", status: "Completed" }
        Server->>Server: Cập nhật DB (Status: Completed)
    end
```