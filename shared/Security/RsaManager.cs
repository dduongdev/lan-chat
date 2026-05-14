using System.Security.Cryptography;

namespace LanChat.Shared.Security
{
    /// <summary>
    /// Quản lý việc tạo khóa RSA và các thao tác mã hóa/giải mã RSA.
    /// Server sử dụng instance này (Singleton) để giữ Private Key.
    /// Client sử dụng phương thức static Encrypt với Public Key nhận được từ Server.
    /// </summary>
    public sealed class RsaManager : IDisposable
    {
        private readonly RSA _rsa;

        /// <summary>
        /// Khởi tạo RsaManager với cặp khóa RSA 2048-bit mới.
        /// </summary>
        public RsaManager()
        {
            _rsa = RSA.Create(2048);
        }

        /// <summary>
        /// Lấy chuỗi XML chứa Public Key.
        /// </summary>
        /// <returns>Chuỗi XML Public Key (không chứa Private Key).</returns>
        public string GetPublicKey()
        {
            return _rsa.ToXmlString(includePrivateParameters: false);
        }

        /// <summary>
        /// Giải mã dữ liệu bằng Private Key (OAEP SHA-256).
        /// Sử dụng tại Server để giải mã AES Key/IV nhận từ Client.
        /// </summary>
        /// <param name="data">Dữ liệu đã mã hóa.</param>
        /// <returns>Dữ liệu gốc sau khi giải mã.</returns>
        public byte[] Decrypt(byte[] data)
        {
            return _rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        /// <summary>
        /// Mã hóa dữ liệu bằng Public Key (OAEP SHA-256).
        /// Sử dụng tại Client để mã hóa AES Key/IV trước khi gửi lên Server.
        /// </summary>
        /// <param name="data">Dữ liệu cần mã hóa.</param>
        /// <param name="publicKeyXml">Chuỗi XML Public Key nhận từ Server.</param>
        /// <returns>Dữ liệu đã mã hóa.</returns>
        public static byte[] Encrypt(byte[] data, string publicKeyXml)
        {
            using RSA rsa = RSA.Create();
            rsa.FromXmlString(publicKeyXml);
            return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
        }

        public void Dispose()
        {
            _rsa.Dispose();
        }
    }
}
