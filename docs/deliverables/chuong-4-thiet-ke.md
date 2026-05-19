# CHƯƠNG 4: THIẾT KẾ CHI TIẾT ỨNG DỤNG

## 4.1 Biểu đồ Use Case tổng thể của ứng dụng

Dựa trên các yêu cầu chức năng đã đề ra, hệ thống LanChat được thiết kế để phục vụ các tương tác nghiệp vụ thời gian thực của người dùng (End-User) trong một mạng cục bộ (LAN). Sơ đồ Usecase dưới đây tổng hợp toàn bộ các hành vi mà ứng dụng cung cấp, được quản lý phi tập trung qua sự điều phối của Server.

```mermaid
flowchart LR
    User((👤 Người dùng))
    Server((🖥️ Hệ thống Server))

    subgraph App ["Hệ thống Ứng dụng LanChat"]
        direction TB
        UC1(["UC-01: Trao đổi khóa bảo mật (Bắt buộc)"])
        UC2(["UC-02: Đăng ký tài khoản"])
        UC3(["UC-03: Đăng nhập"])
        UC4(["UC-04: Xem người dùng Online"])
        UC5(["UC-05: Nhắn tin (Cá nhân & Nhóm)"])
        UC6(["UC-06: Truyền nhận File"])
        UC7(["UC-07: Đăng xuất"])
        UC8(["UC-08: Quản lý nhóm Chat"])
        UC9(["UC-09: Lấy danh sách Chat gần đây"])
        UC10(["UC-10: Tải lịch sử Chat"])
        UC11(["UC-11: Gọi Video (UDP/P2P)"])
    end

    %% Tương tác của Người dùng (Client)
    User --> UC2
    User --> UC3
    User --> UC4
    User --> UC5
    User --> UC6
    User --> UC8
    User --> UC9
    User --> UC10
    User --> UC11
    User --> UC7

    %% Mối quan hệ Include: Handshake là tiền đề của mọi tác vụ mạng
    UC2 -.->|&lt;&lt;include&gt;&gt;| UC1
    UC3 -.->|&lt;&lt;include&gt;&gt;| UC1
    UC4 -.->|&lt;&lt;include&gt;&gt;| UC1
    UC5 -.->|&lt;&lt;include&gt;&gt;| UC1
    UC6 -.->|&lt;&lt;include&gt;&gt;| UC1
    UC8 -.->|&lt;&lt;include&gt;&gt;| UC1
    UC9 -.->|&lt;&lt;include&gt;&gt;| UC1
    UC10 -.->|&lt;&lt;include&gt;&gt;| UC1
    UC11 -.->|&lt;&lt;include&gt;&gt;| UC1
    UC7 -.->|&lt;&lt;include&gt;&gt;| UC1
    
    %% Tương tác của Server
    UC1 --- Server
    UC2 --- Server
    UC3 --- Server
    UC4 --- Server
    UC5 --- Server
    UC6 --- Server
    UC7 --- Server
    UC8 --- Server
    UC9 --- Server
    UC10 --- Server
    UC11 --- Server

    %% CSS Styling
    classDef actor fill:#f3e5f5,stroke:#6a1b9a,stroke-width:2px;
    classDef usecase fill:#e3f2fd,stroke:#1565c0,stroke-width:2px;
    class User,Server actor;
    class UC1,UC2,UC3,UC4,UC5,UC6,UC7,UC8,UC9,UC10,UC11 usecase;
```

## 4.2 Chi tiết các Use Case và Biểu đồ tuần tự (Sequence Diagram)

Do khối lượng Use Case của hệ thống là tương đối lớn, báo cáo này sẽ đi sâu phân tích thiết kế giao tiếp của các nghiệp vụ quan trọng nhất mang tính đại diện cho các phương thức khác nhau: Cơ chế bảo mật (UC-01), Nhắn tin (UC-05), Truyền tải file (UC-06) và Streaming Đa phương tiện qua Peer-to-Peer (UC-11).

### 4.2.1 Trao đổi khóa bảo mật (UC-01: Secure Handshake)

**1. Mô tả chi tiết**
Handshake được tự động kích hoạt ngay khi Client kết nối TCP thành công đến Server, thiết lập một kênh truyền mã hóa AES.
*   Server sẽ giữ vòng đời phân phối RSA Key (`RsaManager`).
*   Client tự sinh ngẫu nhiên AES Key + IV, mã hóa nó bằng RSA Public Key của Server và gửi lên. 
*   Kể từ sau bước này, mọi gói tin được gửi đi đều được mã hóa bằng thuật toán đối xứng AES.

**2. Biểu đồ tuần tự**
```mermaid
sequenceDiagram
    participant Client as LanChat Client
    participant Server as LanChat Server
    
    Client->>Server: Connect TCP (Port 8080)
    Server-->>Client: Connection Accepted
    
    Client->>Server: Packet [RoutingKey="auth.handshake.req"]
    Server->>Server: Kích hoạt RsaManager lấy Public Key
    Server-->>Client: Packet [RoutingKey="auth.handshake.pubkey", HandshakePubKeyPayload]
    
    Client->>Client: Sinh ngẫu nhiên AES Key & IV
    Client->>Client: Mã hóa AES Key theo RSA Public Key
    Client->>Server: Packet [RoutingKey="auth.handshake.res", HandshakeResponsePayload]
    
    Server->>Server: Dùng RSA Private Key giải mã ra AES Key
    Server->>Server: Gán AES Key vào Session.Cipher (IsEncrypted = true)
    Server-->>Client: Packet [RoutingKey="auth.handshake.done"] (Mã hóa bằng AES)
    
    Client->>Client: Cập nhật Session.IsEncrypted = true
    Note over Client, Server: Kênh giao tiếp giờ đây được bảo vệ an toàn 100% bằng AES
```

---

### 4.2.2 Giao tiếp tin nhắn (UC-05: Send Message)

**1. Mô tả chi tiết**
Cho phép gửi tin nhắn văn bản giữa 2 cá nhân (Private) hoặc một hội thoại nhóm (Group). Cấu trúc của Message Dispatcher sẽ điều hướng gói tin vào class `ServerChatMsgHandler` trên Server. Server sẽ đóng vai trò như một Relay: định tuyến gói tin từ người gửi và gửi lại trực tiếp vào Session của danh sách người nhận (sau khi đã lưu trữ vào CSDL).

**2. Biểu đồ tuần tự**
```mermaid
sequenceDiagram
    participant Sender as Client A
    participant Server as LanChat Server
    participant DB as Database (SQLite)
    participant Receiver as Client B (Private) / Clients (Group)

    Sender->>Server: Packet [RoutingKey="chat.msg"] (TargetId, Content)
    Server->>Server: Xác định Message Type (PRIVATE / GROUP)
    Server->>DB: Lưu ChatMessage vào Database
    DB-->>Server: Lưu thành công
    
    Server-->>Sender: Trả về [RoutingKey="chat.echo"] (Để Sender xác nhận đã gửi)
    
    alt Private Chat
        Server->>Server: Tìm Session của Client B
        Server-->>Receiver: Packet [RoutingKey="chat.receive"] (Message Data)
    else Group Chat
        Server->>DB: Truy xuất danh sách Group Members
        DB-->>Server: Trả về Participants
        Server->>Server: Lọc các Session Members đang Online
        Server-->>Receiver: Broadcast [RoutingKey="chat.receive"] tới mọi Clients
    end
```

---

### 4.2.3 Truyền tải tập tin (UC-06: File Transfer)

**1. Mô tả chi tiết**
Truyền file trong LanChat sử dụng cơ chế chia nhỏ luồng byte (Chunking) để tránh việc tắc nghẽn bộ nhớ của TCP Stream. 
*   Người gửi phát tín hiệu Upload. Server sẽ mở File Stream tạm thời để ghi từ từ từng Chunk 4MB.
*   Sau khi Server ghi thành công, Server phát tín hiệu cho người nhận đến lấy (Download). Người nhận tiến hành request lần lượt các Chunk từ Server cho đến khi file hoàn chỉnh.

**2. Biểu đồ tuần tự (Quá trình Upload)**
```mermaid
sequenceDiagram
    participant Sender as Client A
    participant Server as LanChat Server
    
    Sender->>Server: Packet [RoutingKey="file.upload.req"] (Tên file, Hash, Size)
    Server->>Server: Xác thực, mở FileStream
    Server-->>Sender: [RoutingKey="file.upload.res"] (Cấp FileID bắt đầu Upload)
    
    loop Truyền từng đoạn (Chunk 4MB)
        Sender->>Server: [RoutingKey="file.chunk.req"] (FileId, Offset, Bytes)
        Server->>Server: Ghi Stream vào Disk (Hoặc RAM buffer)
        Server-->>Sender: [RoutingKey="file.chunk.res"] (Xác nhận Offset)
    end
    
    Sender->>Server: [RoutingKey="file.complete.req"] (FileId)
    Server->>Server: Checksum & Đóng FileStream
    Server-->>Sender: [RoutingKey="file.complete.res"] (Upload thành công)
```

---

### 4.2.4 Cuộc gọi Video Đa phương tiện qua P2P (UC-11: Video Call)

**1. Mô tả chi tiết**
Tính năng Video Call đánh dấu bước tiến chuyển dịch trạng thái mạng. `TCP port 8080` (đi qua Dispatcher như bình thường) tiếp tục được sử dụng làm vòng điều khiển thông số liên lạc (Signaling Channel). Tuy nhiên, để đáp ứng được độ trễ thời gian thực cho Audio/Video, ứng dụng sẽ khởi động một luồng `UDP Media Plane` độc lập. Server không nhận hay định tuyến bất kỳ file phương tiện nào, mạng luân chuyển dạng lưới P2P (Mesh) hoàn toàn do Client tự quản.

*   **Tạo Gọi (Invitation):** `ServerCallInviteHandler` lọc số người online, quản lý Phiên (SessionManager) lưu vào RAM và phát lời mời bằng TCP.
*   **Phản hồi (Response):** Các Client B trả về `ServerCallResponseHandler` (Accept/Reject). Cập nhật UDP Port cho nhau.
*   **Tham gia (P2P):** Kích hoạt `ClientCallParticipantListHandler` để báo cho Client A biết IP và Port UDP ảo của các thành viên. Video Frame truyền trực diện từ màn hình máy A qua máy B mà không thông qua Server (tối giản latency).

**2. Thiết kế lớp chi tiết (Class Design)**

```mermaid
classDiagram
    class IMessageHandler {
        <<interface>>
        +RoutingKey: string
        +HandleAsync(session: SessionHandler, payload: JsonElement) Task
    }

    class ServerCallInviteHandler {
        +RoutingKey = "call.invite.req"
        +HandleAsync(session, payload) Task
    }

    class ServerCallResponseHandler {
        +RoutingKey = "call.response"
        +HandleAsync(session, payload) Task
    }

    class ServerCallEndHandler {
        +RoutingKey = "call.end"
        +HandleAsync(session, payload) Task
    }

    class ClientCallInviteIncomingHandler {
        +RoutingKey = "call.invite.incoming"
        +HandleAsync(session, payload) Task
    }
    
    class ClientCallParticipantListHandler {
        +RoutingKey = "call.participant.list"
        +HandleAsync(session, payload) Task
    }

    class ServerCallNotifier {
        <<static>>
        +BroadcastParticipantListAsync(session, callId) Task
        +BroadcastEndedAsync(callId) Task
    }

    class CallSessionManager {
        -ConcurrentDictionary _activeCalls
        +CreateSession(callId, type) CallSession
        +AddParticipant(callId, user)
    }

    IMessageHandler <|-- ServerCallInviteHandler
    IMessageHandler <|-- ServerCallResponseHandler
    IMessageHandler <|-- ServerCallEndHandler
    
    IMessageHandler <|-- ClientCallInviteIncomingHandler
    IMessageHandler <|-- ClientCallParticipantListHandler

    ServerCallInviteHandler --> CallSessionManager : Khởi tạo phiên
    ServerCallResponseHandler --> CallSessionManager : Thêm Participant
    ServerCallResponseHandler --> ServerCallNotifier : Gửi thông báo
    ServerCallEndHandler --> CallSessionManager : Đóng phiên Video
```

**3. Biểu đồ tuần tự (Sequence Diagram)**

```mermaid
sequenceDiagram
    participant Caller as Caller (Client A)
    participant Server TCP as Server (Signaling)
    participant Callee as Callee (Client B/Group)

    %% GDD 1
    rect rgb(200, 220, 240)
    note right of Caller: 1. Khởi tạo & Signaling qua TCP
    Caller->>Server TCP: [RoutingKey="call.invite.req"] (TargetId, UdpPort)
    Server TCP->>Caller: [RoutingKey="call.invite.created"] 
    Server TCP->>Callee: [RoutingKey="call.invite.incoming"] (CallId, CallerInfo)
    end

    %% GD 2
    rect rgb(200, 240, 200)
    note right of Callee: 2. Phản hồi và đàm phán UDP Port
    alt Callee CHẤP NHẬN Cuộc gọi (Accept)
        Callee->>Server TCP: [RoutingKey="call.response"] (CallId, Accept=true, UdpPort)
        Server TCP->>Caller: [RoutingKey="call.participant.list"] (Kèm IP & UDP Port)
        Server TCP->>Callee: [RoutingKey="call.participant.list"] (Kèm IP & UDP Port)
        note over Caller, Callee: 3. Media Plane: Mesh P2P UDP Streaming
        Caller<-->>Callee: Gửi Trực Tiếp Video/Audio Frame qua UDP (Không qua Server)
    else Callee TỪ CHỐI Cuộc gọi (Reject)
        Callee->>Server TCP: [RoutingKey="call.response"] (CallId, Accept=false)
        Server TCP->>Caller: [RoutingKey="call.ended"] (Reason="Rejected")
    end
    end

    %% GD 3
    rect rgb(240, 200, 200)
    note right of Caller: 4. Kết thúc & Đóng phiên Video
    Caller->>Server TCP: [RoutingKey="call.end"] (CallId)
    Server TCP->>Server TCP: Xóa CallSessionManager
    Server TCP->>Callee: [RoutingKey="call.ended"] (Đóng cửa sổ Video)
    end
```