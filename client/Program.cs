using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using SimpleTcp;
using LanChat.Messaging;
using LanChat.Client.Handlers;
using LanChat.Client.Handlers.Auth;
using LanChat.Client.Handlers.Chat;
using LanChat.Client.Handlers.File;
using LanChat.Client.Handlers.Presence;
using LanChat.Client.Handlers.User;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string testUser = args.Length > 0 ? args[0] : "testuser_" + Guid.NewGuid().ToString().Substring(0, 4);
            string testPass = args.Length > 1 ? args[1] : "password123";
            string targetUser = args.Length > 2 ? args[2] : "admin"; // For private chat test

            Console.WriteLine($"Starting LanChat Client Test for user: {testUser}");

            // Khởi tạo Dispatcher và đăng ký Handler
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterHandler(new ClientHandshakePubKeyHandler());
            dispatcher.RegisterHandler(new ClientHandshakeDoneHandler());
            dispatcher.RegisterHandler(new ClientRegisterResponseHandler());
            dispatcher.RegisterHandler(new ClientLoginResponseHandler());
            dispatcher.RegisterHandler(new ClientUserPresenceHandler(RoutingKeys.UserJoined));
            dispatcher.RegisterHandler(new ClientUserPresenceHandler(RoutingKeys.UserLeft));
            dispatcher.RegisterHandler(new ClientUserListResponseHandler());
            dispatcher.RegisterHandler(new ClientChatEchoHandler());
            dispatcher.RegisterHandler(new ClientChatReceiveHandler());
            dispatcher.RegisterHandler(new ClientChatHistoryHandler());
            dispatcher.RegisterHandler(new ClientFileOfferHandler());
            dispatcher.RegisterHandler(new ClientFileStartHandler());
            dispatcher.RegisterHandler(new ClientFileStatusHandler());
            dispatcher.RegisterHandler(new ClientGroupCreateResponseHandler());
            dispatcher.RegisterHandler(new ClientGroupListResponseHandler());
            dispatcher.RegisterHandler(new ClientGroupInviteHandler());
            dispatcher.RegisterHandler(new ClientGroupAddResponseHandler());
            dispatcher.RegisterHandler(new ClientGroupMemberAddedHandler());
            dispatcher.RegisterHandler(new ClientGroupLeaveResponseHandler());
            dispatcher.RegisterHandler(new ClientGroupMemberLeftHandler());

            try
            {
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync("127.0.0.1", 8080);
                Console.WriteLine("Connected to server.");

                var simpleClient = new SimpleTcpClient(tcpClient.Client);
                var sessionHandler = new SessionHandler(simpleClient, dispatcher);

                // Run session handler in background
                var sessionTask = sessionHandler.StartAsync();

                // UC-01: Handshake
                Console.WriteLine("Initiating Handshake...");
                await sessionHandler.SendAsync(RoutingKeys.HandshakeReq, new { });

                // Chờ một chút để handshake hoàn thành
                await Task.Delay(1000);

                if (sessionHandler.IsEncrypted)
                {
                    // Start Heartbeat Ping Task
                    _ = Task.Run(async () =>
                    {
                        while (true)
                        {
                            await Task.Delay(30000); // 30 seconds
                            try
                            {
                                await sessionHandler.SendAsync(RoutingKeys.SysPing, new { });
                                Console.WriteLine("[Client] Sent Heartbeat Ping.");
                            }
                            catch { break; }
                        }
                    });

                    // UC-02: Registration
                    Console.WriteLine($"Simulating Registration for {testUser}...");
                    var regPayload = new RegisterRequestPayload { Username = testUser, Password = testPass };
                    await sessionHandler.SendAsync(RoutingKeys.AuthRegisterReq, regPayload);
                    await Task.Delay(1000);

                    // UC-03: Login
                    Console.WriteLine($"Simulating Login for {testUser}...");
                    var loginPayload = new LoginRequestPayload
                    {
                        Username = testUser,
                        Password = testPass
                    };
                    sessionHandler.Username = testUser;
                    await sessionHandler.SendAsync(RoutingKeys.AuthLoginReq, loginPayload);

                    // Chờ đăng nhập hoàn tất
                    await Task.Delay(1000);

                    // UC-04: Get Online Users
                    Console.WriteLine("Simulating Get Online Users request...");
                    await sessionHandler.SendAsync(RoutingKeys.UserListReq, new { });
                    await Task.Delay(500);

                    // UC-05: Chat
                    Console.WriteLine($"Simulating Private Message to {targetUser}...");
                    var chatPayload = new ChatMessagePayload
                    {
                        ClientMessageId = Guid.NewGuid(),
                        TargetType = "PRIVATE",
                        TargetId = targetUser,
                        Content = $"Hello {targetUser}, I am {testUser}! 🎉"
                    };
                    await sessionHandler.SendAsync(RoutingKeys.ChatMsg, chatPayload);
                    await Task.Delay(500);

                    Console.WriteLine("Simulating Broadcast Message...");
                    var broadcastPayload = new ChatMessagePayload
                    {
                        ClientMessageId = Guid.NewGuid(),
                        TargetType = "ALL",
                        TargetId = "",
                        Content = $"Global announcement from {testUser}! 📢"
                    };
                    await sessionHandler.SendAsync(RoutingKeys.ChatMsg, broadcastPayload);
                    await Task.Delay(500);

                    // UC-05: History
                    Console.WriteLine($"Simulating History Request for chat with {targetUser}...");
                    var historyReq = new ChatHistoryRequestPayload
                    {
                        TargetId = targetUser,
                        Limit = 10
                    };
                    await sessionHandler.SendAsync(RoutingKeys.ChatHistoryReq, historyReq);
                    await Task.Delay(500);

                    // UC-06: File Transfer
                    // Tạo file test để gửi
                    string testFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"testfile_{testUser}.txt");
                    System.IO.File.WriteAllText(testFilePath, $"This is a test file from {testUser}. Content: Hello World! 🎉\nTimestamp: {DateTime.UtcNow}");
                    var fileInfo = new System.IO.FileInfo(testFilePath);

                    Console.WriteLine($"Simulating File Transfer to {targetUser}...");
                    var fileReqPayload = new FileRequestPayload
                    {
                        ReceiverUsername = targetUser,
                        FileName = fileInfo.Name,
                        FileSize = fileInfo.Length
                    };

                    // Lưu metadata: ghi đường dẫn file để Sender biết đọc từ đâu
                    // Ta chưa biết FileTransferId, nhưng khi nhận FileStartPayload,
                    // Sender sẽ dựa vào file_path metadata
                    // Workaround: lưu file path theo tên người nhận
                    sessionHandler.SetMetadata($"pending_file_path", testFilePath);
                    await sessionHandler.SendAsync(RoutingKeys.FileRequest, fileReqPayload);
                    await Task.Delay(500);

                    // UC-08: Group Management
                    if (testUser == "userA")
                    {
                        Console.WriteLine("Simulating Group Creation...");
                        var groupCreatePayload = new GroupCreateRequestPayload
                        {
                            GroupName = "Avengers Team",
                            InitialMembers = new System.Collections.Generic.List<string> { targetUser }
                        };
                        await sessionHandler.SendAsync(RoutingKeys.GroupCreateReq, groupCreatePayload);
                        await Task.Delay(1000);
                    }

                    Console.WriteLine("Simulating Get Group List request...");
                    await sessionHandler.SendAsync(RoutingKeys.GroupListReq, new { });
                    await Task.Delay(500);
                }
                else
                {
                    Console.WriteLine("Failed to encrypt channel.");
                }

                // Chờ thêm 15 giây để nhận các gói tin đến (File Offer, File Start, ChatReceive...)
                Console.WriteLine("Waiting for incoming messages (15s)...");
                await Task.Delay(15000);

                // UC-07: Logout
                if (!string.IsNullOrEmpty(sessionHandler.Username))
                {
                    Console.WriteLine($"Simulating Logout for {testUser}...");
                    await sessionHandler.SendAsync(RoutingKeys.AuthLogoutReq, new { });
                    await Task.Delay(1000); // Đợi server xử lý
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            Console.WriteLine("Client terminated. Press any key to exit.");
        }
    }
}
