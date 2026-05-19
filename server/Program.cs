using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpleTcp;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.Handlers;
using LanChat.Server.Handlers.Auth;
using LanChat.Server.Handlers.Call;
using LanChat.Server.Handlers.Chat;
using LanChat.Server.Handlers.File;
using LanChat.Server.Handlers.User;
using LanChat.Server.Security;
using LanChat.Server.State;
using LanChat.Server.Transfers;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting LanChat Server...");

            // Cấu hình Dependency Injection
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite("Data Source=lanchat.db"));
            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddSingleton<SessionManager>();
            services.AddSingleton<CallSessionManager>();

            var serviceProvider = services.BuildServiceProvider();

            // Đảm bảo Database được tạo
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.EnsureCreated();
                Console.WriteLine("Database ensured created.");
            }

            // Lấy các Singleton services
            var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
            var sessionManager = serviceProvider.GetRequiredService<SessionManager>();
            var callSessionManager = serviceProvider.GetRequiredService<CallSessionManager>();

            // UC-06 v2: Khởi tạo FileStreamManager (Data Plane - Port 8081)
            var fileStreamManager = new FileStreamManager(serviceProvider, sessionManager);
            fileStreamManager.Start();

            // Khởi tạo Dispatcher và đăng ký Handler
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterHandler(new ServerHandshakeReqHandler());
            dispatcher.RegisterHandler(new ServerHandshakeResHandler());
            dispatcher.RegisterHandler(new ServerRegisterHandler(serviceProvider, passwordHasher));
            dispatcher.RegisterHandler(new ServerLoginHandler(serviceProvider, passwordHasher, sessionManager));
            dispatcher.RegisterHandler(new ServerUserListHandler(sessionManager));
            dispatcher.RegisterHandler(new ServerRecentChatsHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerChatHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerChatHistoryHandler(serviceProvider));

            // UC-06 v2: Đăng ký handler mới
            dispatcher.RegisterHandler(new ServerFileUploadHandler(serviceProvider, sessionManager, fileStreamManager));
            dispatcher.RegisterHandler(new ServerFileDownloadHandler(serviceProvider, sessionManager, fileStreamManager));

            dispatcher.RegisterHandler(new ServerLogoutHandler(sessionManager));
            dispatcher.RegisterHandler(new ServerHeartbeatHandler());
            dispatcher.RegisterHandler(new ServerGroupListHandler(serviceProvider));
            dispatcher.RegisterHandler(new ServerGroupCreateHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerGroupAddHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerGroupLeaveHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerCallInviteHandler(serviceProvider, sessionManager, callSessionManager));
            dispatcher.RegisterHandler(new ServerCallResponseHandler(sessionManager, callSessionManager));
            dispatcher.RegisterHandler(new ServerCallMediaStateHandler(sessionManager, callSessionManager));
            dispatcher.RegisterHandler(new ServerCallEndHandler(sessionManager, callSessionManager));

            var tcpListener = new TcpListener(IPAddress.Any, 8080);
            tcpListener.Start();
            Console.WriteLine("Server is listening on port 8080 (Control Plane)...");

            // SessionJanitor (Background Service)
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(60)); // Chạy mỗi phút

                    var now = DateTime.UtcNow;
                    foreach (var sessionPair in sessionManager.Snapshot())
                    {
                        var session = sessionPair.Value;
                        if ((now - session.LastActivityTime).TotalSeconds > 90)
                        {
                            Console.WriteLine($"[SessionJanitor] User '{session.Username}' timed out. Disconnecting...");
                            await sessionManager.HandleDisconnectAsync(session);
                        }
                    }
                }
            });

            // UC-06 v2: FileJanitor (Background Service)
            _ = Task.Run(async () => await FileJanitorService.RunAsync(serviceProvider, fileStreamManager));

            while (true)
            {
                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                Console.WriteLine($"Client connected: {tcpClient.Client.RemoteEndPoint}");

                var simpleClient = new SimpleTcpClient(tcpClient.Client);
                var sessionHandler = new SessionHandler(simpleClient, dispatcher);
                
                _ = Task.Run(async () => {
                    try
                    {
                        await sessionHandler.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Session disconnected: {ex.Message}");
                    }
                    finally
                    {
                        var username = sessionHandler.Username;
                        // Khi vòng lặp của session kết thúc (do lỗi, ngắt kết nối, hay logout)
                        // Luôn gọi đến phương thức dọn dẹp duy nhất.
                        await sessionManager.HandleDisconnectAsync(sessionHandler);

                        if (!string.IsNullOrEmpty(username))
                        {
                            var affectedCalls = callSessionManager.RemoveUserFromAllCalls(username);
                            foreach (var callId in affectedCalls)
                            {
                                var remaining = callSessionManager.GetParticipantUsernames(callId);
                                await ServerCallNotifier.BroadcastParticipantLeftAsync(sessionManager, remaining, callId, username, "Disconnected");

                                if (callSessionManager.GetParticipantCount(callId) < 2)
                                {
                                    await ServerCallNotifier.BroadcastEndedAsync(callSessionManager, sessionManager, callId, "NotEnoughParticipants", remaining);
                                    callSessionManager.EndCall(callId);
                                }
                                else
                                {
                                    await ServerCallNotifier.BroadcastParticipantListAsync(callSessionManager, sessionManager, callId);
                                }
                            }
                        }
                    }
                });
            }
        }
    }
}
