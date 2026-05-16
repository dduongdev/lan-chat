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

            await session.SendAsync(RoutingKeys.HandshakeRes, resPayload);

            // 5. Lưu AesCipher vào SessionHandler để sử dụng cho các message sau.
            session.Cipher = aesCipher; 
        }
    }
}
