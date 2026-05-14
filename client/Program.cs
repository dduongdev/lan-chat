using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using SimpleTcp;
using LanChat.Messaging;
using LanChat.Client.Handlers;
using LanChat.Client.Handlers.Auth;
using LanChat.Client.Handlers.Presence;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Client
{
    class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting LanChat Client Test...");

            // Khởi tạo Dispatcher và đăng ký Handler
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterHandler(new ClientHandshakePubKeyHandler());
            dispatcher.RegisterHandler(new ClientHandshakeDoneHandler());
            dispatcher.RegisterHandler(new ClientRegisterResponseHandler());
            dispatcher.RegisterHandler(new ClientLoginResponseHandler());
            dispatcher.RegisterHandler(new ClientUserPresenceHandler(RoutingKeys.UserJoined));
            dispatcher.RegisterHandler(new ClientUserPresenceHandler(RoutingKeys.UserLeft));

            try
            {
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync("127.0.0.1", 8080);
                Console.WriteLine("Connected to server.");

                var simpleClient = new SimpleTcpClient(tcpClient.Client);
                var sessionHandler = new SessionHandler(simpleClient, dispatcher);

                // Run session handler in background
                var sessionTask = sessionHandler.StartAsync();

                // Trigger handshake (StartHandshakeAsync)
                Console.WriteLine("Initiating Handshake...");
                await sessionHandler.SendAsync(RoutingKeys.HandshakeReq, new { });

                // Chờ một chút để handshake hoàn thành
                await Task.Delay(1000);

                if (sessionHandler.IsEncrypted)
                {
                    Console.WriteLine("Channel is encrypted. Simulating User Login...");
                    var loginPayload = new LoginRequestPayload
                    {
                        Username = "testuser",
                        Password = "password123"
                    };
                    await sessionHandler.SendAsync(RoutingKeys.AuthLoginReq, loginPayload);
                }
                else
                {
                    Console.WriteLine("Failed to encrypt channel.");
                }

                // Wait for the session to run (it blocks until disconnected)
                await sessionTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            Console.WriteLine("Client terminated. Press any key to exit.");
            Console.ReadKey();
        }
    }
}
