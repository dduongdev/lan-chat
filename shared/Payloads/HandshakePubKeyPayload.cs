namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload chứa Public Key của Server gửi cho Client.
    /// RoutingKey: auth.handshake.pubkey
    /// </summary>
    public class HandshakePubKeyPayload
    {
        /// <summary>
        /// Chuỗi XML chứa khóa công khai RSA của Server.
        /// </summary>
        public string RsaPublicKey { get; set; } = string.Empty;
    }
}
