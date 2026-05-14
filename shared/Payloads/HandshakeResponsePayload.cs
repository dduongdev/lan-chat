namespace LanChat.Shared.Payloads
{
    /// <summary>
    /// Payload chứa khóa AES đã được Client mã hóa bằng RSA và gửi lên Server.
    /// RoutingKey: auth.handshake.res
    /// </summary>
    public class HandshakeResponsePayload
    {
        /// <summary>
        /// Khóa AES đã được mã hóa bằng RSA Public Key của Server.
        /// </summary>
        public byte[] EncryptedAesKey { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Vector khởi tạo (IV) của AES đã được mã hóa bằng RSA Public Key của Server.
        /// </summary>
        public byte[] EncryptedAesIV { get; set; } = Array.Empty<byte>();
    }
}
