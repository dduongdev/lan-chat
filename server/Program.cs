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
using LanChat.Server.Handlers.User;
using LanChat.Server.Security;
using LanChat.Server.State;
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

            // Khởi tạo Dispatcher và đăng ký Handler
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterHandler(new ServerHandshakeReqHandler());
            dispatcher.RegisterHandler(new ServerHandshakeResHandler());
            dispatcher.RegisterHandler(new ServerRegisterHandler(serviceProvider, passwordHasher));
            dispatcher.RegisterHandler(new ServerLoginHandler(serviceProvider, passwordHasher, sessionManager));
            dispatcher.RegisterHandler(new ServerUserListHandler(sessionManager));
            dispatcher.RegisterHandler(new ServerChatHandler(serviceProvider, sessionManager));
            dispatcher.RegisterHandler(new ServerChatHistoryHandler(serviceProvider));

            var tcpListener = new TcpListener(IPAddress.Any, 8080);
            tcpListener.Start();
            Console.WriteLine("Server is listening on port 8080...");

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
                        // Khi session kết thúc, nếu user đã đăng nhập, ta xóa khỏi SessionManager và broadcast
                        if (!string.IsNullOrEmpty(sessionHandler.Username))
                        {
                            if (sessionManager.TryRemove(sessionHandler.Username, out _))
                            {
                                Console.WriteLine($"[Server] User '{sessionHandler.Username}' disconnected.");
                                
                                // Broadcast UserLeft
                                var leftPayload = new UserPresencePayload { Username = sessionHandler.Username };
                                foreach (var otherSession in sessionManager.Snapshot())
                                {
                                    try
                                    {
                                        await otherSession.Value.SendAsync(RoutingKeys.UserLeft, leftPayload);
                                    }
                                    catch { /* Ignore */ }
                                }
                            }
                        }
                    }
                });
            }
        }
    }
}
