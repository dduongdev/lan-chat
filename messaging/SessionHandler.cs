using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LanChat.Shared.Security;
using SimpleTcp;
using SimpleTcp.Exceptions;

namespace LanChat.Messaging
{
    public class SessionHandler
    {
        private readonly ISimpleTcpClient _client;
        private readonly MessageDispatcher _dispatcher;
        private bool _isRunning;

        public ISimpleTcpClient TcpClient => _client;

        public AesCipher? Cipher { get; set; }

        public bool IsEncrypted => Cipher != null;

        /// <summary>
        /// Định danh của người dùng đã được xác thực trong phiên làm việc này.
        /// Sẽ có giá trị sau khi Đăng nhập thành công.
        /// </summary>
        public string? Username { get; set; }

        public SessionHandler(ISimpleTcpClient client, MessageDispatcher dispatcher)
        {
            _client = client;
            _dispatcher = dispatcher;
        }

        public async Task StartAsync(CancellationToken ct = default)
        {
            _isRunning = true;
            try
            {
                while (_isRunning && !ct.IsCancellationRequested)
                {
                    var envelope = await _client.ReceiveObjectAsync<MessageEnvelope>();
                    if (envelope == null) continue;

                    if (IsEncrypted && envelope.Payload.ValueKind == JsonValueKind.String)
                    {
                        string encryptedPayload = envelope.Payload.GetString() ?? string.Empty;
                        string decryptedJson = Cipher!.Decrypt(encryptedPayload);
                        using var doc = JsonDocument.Parse(decryptedJson);
                        envelope.Payload = doc.RootElement.Clone();
                    }
                    
                    await _dispatcher.DispatchAsync(this, envelope);
                }
            }
            catch (ConnectionClosedException)
            {
                Console.WriteLine("Client disconnected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Session error: {ex.Message}");
            }
            finally
            {
                _client.Close();
            }
        }

        public async Task SendAsync<T>(string routingKey, T payload)
        {
            MessageEnvelope envelope;

            if (IsEncrypted)
            {
                string plainJson = JsonSerializer.Serialize(payload);
                string encryptedBase64 = Cipher!.Encrypt(plainJson);
                envelope = MessageEnvelope.Create(routingKey, encryptedBase64);
            }
            else
            {
                envelope = MessageEnvelope.Create(routingKey, payload);
            }

            await _client.SendObjectAsync(envelope);
        }

        public void Stop() => _isRunning = false;
    }
}