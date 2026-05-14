using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;
using LanChat.Shared.Security;

namespace LanChat.Server.Handlers
{
    public class ServerHandshakeResHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.HandshakeRes;

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            try
            {
                Console.WriteLine($"[Server] Received HandshakeRes. Decrypting AES Key & IV...");

                // 1. Nhận gói tin chứa AES Key & IV đã mã hóa.
                var resPayload = payload.Deserialize<HandshakeResponsePayload>();
                if (resPayload == null)
                {
                    throw new Exception("Payload is null.");
                }

                // 2. Dùng RsaManager để giải mã.
                byte[] decryptedKey = ServerSecurityState.RsaManager.Decrypt(resPayload.EncryptedAesKey);
                byte[] decryptedIV = ServerSecurityState.RsaManager.Decrypt(resPayload.EncryptedAesIV);

                // 3. Tạo một đối tượng AesCipher mới từ Key và IV đã giải mã.
                var aesCipher = new AesCipher(decryptedKey, decryptedIV);

                // 4. Gán đối tượng AesCipher này vào session.Cipher.
                session.Cipher = aesCipher;
                Console.WriteLine($"[Server] AES Key established. Channel is now SECURE.");

                // 5. Gửi thông báo auth.handshake.done về Client
                // (lúc này, session.SendAsync sẽ tự động mã hóa gói tin này bằng AES).
                await session.SendAsync(RoutingKeys.HandshakeDone, "Handshake successful");
            }
            catch (CryptographicException ex)
            {
                Console.WriteLine($"Handshake failed: Cryptographic error - {ex.Message}");
                session.TcpClient.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Handshake error: {ex.Message}");
                session.TcpClient.Close();
            }
        }
    }
}
