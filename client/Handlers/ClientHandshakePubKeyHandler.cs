using System;
using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;
using LanChat.Shared.Security;

namespace LanChat.Client.Handlers
{
    public class ClientHandshakePubKeyHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.HandshakePubKey;

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Client] Received Server's Public Key. Generating AES Key...");

            // 1. Nhận Public Key của Server.
            var pubKeyPayload = payload.Deserialize<HandshakePubKeyPayload>();
            if (pubKeyPayload == null || string.IsNullOrEmpty(pubKeyPayload.RsaPublicKey))
            {
                Console.WriteLine("Error: Invalid HandshakePubKeyPayload.");
                return;
            }

            // 2. Tạo một đối tượng AesCipher mới (tự sinh Key & IV ngẫu nhiên).
            var aesCipher = new AesCipher();

            // 3. Dùng RsaManager.Encrypt() để mã hóa Key và IV vừa tạo bằng Public Key của Server.
            Console.WriteLine($"[Client] Encrypting AES Key with Server's RSA Public Key and sending back...");
            byte[] encryptedKey = RsaManager.Encrypt(aesCipher.Key, pubKeyPayload.RsaPublicKey);
            byte[] encryptedIV = RsaManager.Encrypt(aesCipher.IV, pubKeyPayload.RsaPublicKey);

            // 4. Đóng gói vào HandshakeResponsePayload.
            var resPayload = new HandshakeResponsePayload
            {
                EncryptedAesKey = encryptedKey,
                EncryptedAesIV = encryptedIV
            };

            // 5. Gán đối tượng AesCipher vào session.Cipher của Client ngay lập tức.
            session.Cipher = aesCipher;

            // 6. Gửi gói tin auth.handshake.res lên Server.
            // Vì session.Cipher đã được gán, lệnh SendAsync này đáng lẽ sẽ mã hóa gói tin.
            // Tuy nhiên, theo luồng, gói auth.handshake.res chứa khóa gửi cho Server nên KHÔNG được mã hóa AES (vì Server chưa có khóa).
            // Do đó, ta tạm gỡ Cipher, gửi đi, rồi gán lại Cipher.
            
            session.Cipher = null; 
            await session.SendAsync(RoutingKeys.HandshakeRes, resPayload);
            session.Cipher = aesCipher; 
        }
    }
}
