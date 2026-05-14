using System.Security.Cryptography;
using System.Text;

namespace LanChat.Shared.Crypto
{
    /// <summary>
    /// Tiện ích mã hóa/giải mã AES-256
    /// </summary>
    public static class AesCipher
    {
        /// <summary>
        /// Tạo cặp khóa AES mới (key + IV)
        /// </summary>
        /// <returns>Tuple chứa (Base64Key, Base64IV)</returns>
        public static (string Key, string IV) GenerateKeyAndIV()
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();

                return (
                    Convert.ToBase64String(aes.Key),
                    Convert.ToBase64String(aes.IV)
                );
            }
        }

        /// <summary>
        /// Mã hóa plaintext bằng AES-256
        /// </summary>
        public static string Encrypt(string plaintext, string base64Key, string base64IV)
        {
            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = Convert.FromBase64String(base64IV);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plaintextBytes, 0, plaintextBytes.Length);
                        cs.FlushFinalBlock();
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }

        /// <summary>
        /// Giải mã ciphertext bằng AES-256
        /// </summary>
        public static string Decrypt(string ciphertext, string base64Key, string base64IV)
        {
            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = Convert.FromBase64String(base64IV);
            byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(ciphertextBytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
