using System.Security.Cryptography;
using System.Text;

namespace LanChat.Shared.Crypto
{
    /// <summary>
    /// Tiện ích mã hóa/giải mã RSA-2048
    /// </summary>
    public static class RsaManager
    {
        /// <summary>
        /// Tạo cặp khóa RSA mới
        /// </summary>
        /// <returns>Tuple chứa (PublicKeyXml, PrivateKeyXml)</returns>
        public static (string PublicKey, string PrivateKey) GenerateKeyPair()
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                string publicKey = rsa.ToXmlString(false);
                string privateKey = rsa.ToXmlString(true);
                return (publicKey, privateKey);
            }
        }

        /// <summary>
        /// Mã hóa plaintext bằng public key (RSA)
        /// </summary>
        public static string Encrypt(string plaintext, string publicKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(publicKeyXml);
                
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                byte[] encryptedBytes = rsa.Encrypt(plaintextBytes, true);
                
                return Convert.ToBase64String(encryptedBytes);
            }
        }

        /// <summary>
        /// Giải mã ciphertext bằng private key (RSA)
        /// </summary>
        public static string Decrypt(string ciphertext, string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(privateKeyXml);
                
                byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);
                byte[] decryptedBytes = rsa.Decrypt(ciphertextBytes, true);
                
                return Encoding.UTF8.GetString(decryptedBytes);
            }
        }

        /// <summary>
        /// Ký dữ liệu bằng private key
        /// </summary>
        public static string Sign(string data, string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(privateKeyXml);
                
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                byte[] signature = rsa.SignData(dataBytes, CryptoConfig.MapNameToOID("SHA256"));
                
                return Convert.ToBase64String(signature);
            }
        }

        /// <summary>
        /// Xác minh chữ ký bằng public key
        /// </summary>
        public static bool VerifySignature(string data, string signature, string publicKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(publicKeyXml);
                
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                byte[] signatureBytes = Convert.FromBase64String(signature);
                
                return rsa.VerifyData(dataBytes, CryptoConfig.MapNameToOID("SHA256"), signatureBytes);
            }
        }
    }
}
