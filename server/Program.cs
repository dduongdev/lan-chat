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
using LanChat.Server.Security;
using LanChat.Server.State;

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

            var serviceProvider = services.BuildServiceProvider();

            // Đảm bảo Database được tạo
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.EnsureCreated();
                Console.WriteLine("Database ensured created.");
            }

            // Lấy DbContext và Hasher
            var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();

            // Khởi tạo Dispatcher và đăng ký Handler
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterHandler(new ServerHandshakeReqHandler());
            dispatcher.RegisterHandler(new ServerHandshakeResHandler());
            
            // Register UC-02 Registration Handler
            dispatcher.RegisterHandler(new ServerRegisterHandler(serviceProvider, passwordHasher));

            var tcpListener = new TcpListener(IPAddress.Any, 8080);
            tcpListener.Start();
            Console.WriteLine("Server is listening on port 8080...");

            var sessionManager = new SessionManager();

            while (true)
            {
                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                Console.WriteLine($"Client connected: {tcpClient.Client.RemoteEndPoint}");

                var simpleClient = new SimpleTcpClient(tcpClient.Client);
                var sessionHandler = new SessionHandler(simpleClient, dispatcher);
                
                // For test purpose, we just run the session
                _ = Task.Run(async () => {
                    await sessionHandler.StartAsync();
                    Console.WriteLine("Session ended.");
                });
            }
        }
    }
}
