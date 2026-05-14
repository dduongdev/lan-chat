using System.Security.Cryptography;
using System.Text;

namespace LanChat.Shared.Security
{
    /// <summary>
    /// Quản lý mã hóa/giải mã AES-256-CBC cho dữ liệu nghiệp vụ sau Handshake.
    /// Client tạo instance mới (tự sinh Key/IV), Server tạo instance từ Key/IV đã giải mã.
    /// </summary>
    public sealed class AesCipher : IDisposable
    {
        private readonly Aes _aes;

        /// <summary>
        /// Khởi tạo AesCipher với Key và IV ngẫu nhiên (AES-256).
        /// Sử dụng tại Client để tạo khóa mới.
        /// </summary>
        public AesCipher()
        {
            _aes = Aes.Create();
            _aes.KeySize = 256;
            _aes.Mode = CipherMode.CBC;
            _aes.Padding = PaddingMode.PKCS7;
            _aes.GenerateKey();
            _aes.GenerateIV();
        }

        /// <summary>
        /// Khởi tạo AesCipher từ Key và IV đã biết.
        /// Sử dụng tại Server sau khi giải mã Key/IV từ Client.
        /// </summary>
        /// <param name="key">AES Key (32 bytes cho AES-256).</param>
        /// <param name="iv">AES IV (16 bytes).</param>
        public AesCipher(byte[] key, byte[] iv)
        {
            _aes = Aes.Create();
            _aes.KeySize = 256;
            _aes.Mode = CipherMode.CBC;
            _aes.Padding = PaddingMode.PKCS7;
            _aes.Key = key;
            _aes.IV = iv;
        }

        /// <summary>
        /// Lấy AES Key hiện tại.
        /// </summary>
        public byte[] Key => _aes.Key;

        /// <summary>
        /// Lấy AES IV hiện tại.
        /// </summary>
        public byte[] IV => _aes.IV;

        /// <summary>
        /// Mã hóa chuỗi plaintext thành chuỗi Base64.
        /// </summary>
        /// <param name="plainText">Chuỗi JSON cần mã hóa.</param>
        /// <returns>Chuỗi Base64 chứa dữ liệu đã mã hóa.</returns>
        public string Encrypt(string plainText)
        {
            using ICryptoTransform encryptor = _aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }

        /// <summary>
        /// Giải mã chuỗi Base64 về chuỗi plaintext.
        /// </summary>
        /// <param name="cipherText">Chuỗi Base64 chứa dữ liệu đã mã hóa.</param>
        /// <returns>Chuỗi JSON gốc sau khi giải mã.</returns>
        public string Decrypt(string cipherText)
        {
            using ICryptoTransform decryptor = _aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public void Dispose()
        {
            _aes.Dispose();
        }
    }
}
