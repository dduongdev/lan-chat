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
            services.AddSingleton<FileTransferManager>();

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
            var fileTransferManager = serviceProvider.GetRequiredService<FileTransferManager>();

            // Khởi tạo Dispatcher và đăng ký Handler
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterHandler(new ServerHandshakeReqHandler());
            dispatcher.RegisterHandler(new ServerHandshakeResHandler());
            dispatcher.RegisterHandler(new ServerRegisterHandler(serviceProvider, passwordHasher));
            dispatcher.RegisterHandler(new ServerLoginHandler(serviceProvider, passwordHasher, sessionManager));
            dispatcher.RegisterHandler(new ServerUserListHandler(sessionManager));
            dispatcher.RegisterHandler(new ServerChatHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerChatHistoryHandler(serviceProvider));
            dispatcher.RegisterHandler(new ServerFileRequestHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerFileResponseHandler(serviceProvider, sessionManager, fileTransferManager));
            dispatcher.RegisterHandler(new ServerFileStatusHandler(serviceProvider));
            dispatcher.RegisterHandler(new ServerLogoutHandler(sessionManager));
            dispatcher.RegisterHandler(new ServerHeartbeatHandler());
            dispatcher.RegisterHandler(new ServerGroupListHandler(serviceProvider));
            dispatcher.RegisterHandler(new ServerGroupCreateHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerGroupAddHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerGroupLeaveHandler(serviceProvider, sessionManager));

            var tcpListener = new TcpListener(IPAddress.Any, 8080);
            tcpListener.Start();
            Console.WriteLine("Server is listening on port 8080...");

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

            while (true)
            {
                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                Console.WriteLine($"Client connected: {tcpClient.Client.RemoteEndPoint}");

                var simpleClient = new SimpleTcpClient(tcpClient.Client);
                var sessionHandler = new SessionHandler(simpleClient, dispatcher);
                
                // For test purpose, we just run the session
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
                        // Khi vòng lặp của session kết thúc (do lỗi, ngắt kết nối, hay logout)
                        // Luôn gọi đến phương thức dọn dẹp duy nhất.
                        await sessionManager.HandleDisconnectAsync(sessionHandler);
                    }
                });
            }
        }
    }
}
