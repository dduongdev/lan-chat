using System.Text.Json;
using System.Threading.Tasks;
using LanChat.Messaging;
using LanChat.Server.State;
using LanChat.Shared.Constants;
using LanChat.Shared.Payloads;

namespace LanChat.Server.Handlers
{
    public class ServerHandshakeReqHandler : IMessageHandler
    {
        public string RoutingKey => RoutingKeys.HandshakeReq;

        public async Task HandleAsync(SessionHandler session, JsonElement payload)
        {
            Console.WriteLine($"[Server] Received HandshakeReq. Sending Public Key...");
            
            // 1. Nhận yêu cầu từ Client.
            // 2. Lấy Public Key từ RsaManager (Singleton).
            string publicKey = ServerSecurityState.RsaManager.GetPublicKey();

            // 3. Đóng gói vào HandshakePubKeyPayload.
            var resPayload = new HandshakePubKeyPayload
            {
                RsaPublicKey = publicKey
            };

            // 4. Gửi trả Client qua session.SendAsync().
            await session.SendAsync(RoutingKeys.HandshakePubKey, resPayload);
        }
    }
}
