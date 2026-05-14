using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.Security;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.Auth
{
    public class ServerLoginHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPasswordHasher _passwordHasher;
        private readonly SessionManager _sessionManager;

        public string RoutingKey => RoutingKeys.AuthLoginReq;

        public ServerLoginHandler(IServiceProvider serviceProvider, IPasswordHasher passwordHasher, SessionManager sessionManager)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine("[Server] Received Login Request.");
            try
            {
                var request = payload.Deserialize<LoginRequestPayload>();
                if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    await session.SendAsync(RoutingKeys.AuthLoginRes, new LoginResponsePayload
                    {
                        Success = false,
                        Message = "Thông tin đăng nhập không hợp lệ."
                    });
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 1. Tìm User trong DB
                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());
                
                // 2. Xác thực
                if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                {
                    Console.WriteLine($"[Server] Login failed for user '{request.Username}'. Invalid credentials.");
                    await session.SendAsync(RoutingKeys.AuthLoginRes, new LoginResponsePayload
                    {
                        Success = false,
                        Message = "Sai tên đăng nhập hoặc mật khẩu."
                    });
                    return;
                }

                // 3. Đăng nhập thành công (Main Flow)
                session.Username = user.Username;
                _sessionManager.AddOrUpdateSession(user.Username, session);

                Console.WriteLine($"[Server] User '{user.Username}' logged in successfully.");
                await session.SendAsync(RoutingKeys.AuthLoginRes, new LoginResponsePayload
                {
                    Success = true,
                    Message = "Đăng nhập thành công."
                });

                // 4. Broadcast (UC-03.7) cho các user khác
                var joinedPayload = new UserPresencePayload { Username = user.Username };
                foreach (var otherSession in _sessionManager.Snapshot())
                {
                    // Tránh gửi cho chính user vừa đăng nhập
                    if (otherSession.Key != user.Username)
                    {
                        try
                        {
                            await otherSession.Value.SendAsync(RoutingKeys.UserJoined, joinedPayload);
                        }
                        catch { /* Ignore error if broadcast fails */ }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error processing login request: {ex.Message}");
                await session.SendAsync(RoutingKeys.AuthLoginRes, new LoginResponsePayload
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi hệ thống, vui lòng thử lại sau."
                });
            }
        }
    }
}
