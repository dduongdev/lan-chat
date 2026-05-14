using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LanChat.Messaging;
using LanChat.Server.Data;
using LanChat.Server.Entities;
using LanChat.Server.Security;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers.Auth
{
    public class ServerRegisterHandler : IMessageHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPasswordHasher _passwordHasher;

        public string RoutingKey => RoutingKeys.AuthRegisterReq;

        public ServerRegisterHandler(IServiceProvider serviceProvider, IPasswordHasher passwordHasher)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        }

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine("[Server] Received Register Request.");
            try
            {
                // 1. Deserialize Payload
                var request = payload.Deserialize<RegisterRequestPayload>();
                if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    await session.SendAsync(RoutingKeys.AuthRegisterRes, new RegisterResponsePayload
                    {
                        Success = false,
                        Message = "Thông tin đăng ký không hợp lệ."
                    });
                    return;
                }

                // Create Scope to resolve DbContext
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 2. Kiểm tra Username
                var usernameExists = await dbContext.Users
                    .AnyAsync(u => u.Username.ToLower() == request.Username.ToLower());

                // 3. Xử lý nếu Username tồn tại
                if (usernameExists)
                {
                    Console.WriteLine($"[Server] Register failed. Username '{request.Username}' already exists.");
                    await session.SendAsync(RoutingKeys.AuthRegisterRes, new RegisterResponsePayload
                    {
                        Success = false,
                        Message = "Tên đăng nhập đã tồn tại."
                    });
                    return;
                }

                // 4. Xử lý đăng ký mới
                string passwordHash = _passwordHasher.HashPassword(request.Password);
                var newUser = new Entities.User
                {
                    Id = Guid.NewGuid(),
                    Username = request.Username,
                    PasswordHash = passwordHash
                };

                dbContext.Users.Add(newUser);
                await dbContext.SaveChangesAsync();

                Console.WriteLine($"[Server] User '{request.Username}' registered successfully.");
                await session.SendAsync(RoutingKeys.AuthRegisterRes, new RegisterResponsePayload
                {
                    Success = true,
                    Message = "Đăng ký thành công."
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error processing register request: {ex.Message}");
                await session.SendAsync(RoutingKeys.AuthRegisterRes, new RegisterResponsePayload
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi hệ thống, vui lòng thử lại sau."
                });
            }
        }
    }
}
