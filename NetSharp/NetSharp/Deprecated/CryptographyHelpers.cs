using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NetSharp.Deprecated
{
    internal static class CryptographyHelpers
    {
        #region Settings

        private static string _hash = "SHA1";
        private static int _iterations = 2;
        private static int _keySize = 256;
        private static string _salt = "aselrias38490a32"; // Random
        private static string _vector = "8947az34awl34kjq"; // Random

        #endregion Settings

        public static string Decrypt(byte[] value, string password)
        {
            return Decrypt<AesManaged>(value, password);
        }

        public static string Decrypt<T>(byte[] value, string password) where T : SymmetricAlgorithm, new()
        {
            byte[] vectorBytes = Encoding.ASCII.GetBytes(_vector); // GetBytes<ASCIIEncoding>(_vector);
            byte[] saltBytes = Encoding.ASCII.GetBytes(_salt); // GetBytes<ASCIIEncoding>(_salt);
            byte[] valueBytes = value;

            byte[] decrypted;
            int decryptedByteCount = 0;

            using (T cipher = new T())
            {
                PasswordDeriveBytes _passwordBytes = new PasswordDeriveBytes(password, saltBytes, _hash, _iterations);
                byte[] keyBytes = _passwordBytes.GetBytes(_keySize / 8);

                cipher.Mode = CipherMode.CBC;

                try
                {
                    using (ICryptoTransform decryptor = cipher.CreateDecryptor(keyBytes, vectorBytes))
                    {
                        using MemoryStream from = new MemoryStream(valueBytes);
                        using CryptoStream reader = new CryptoStream(@from, decryptor, CryptoStreamMode.Read);

                        decrypted = new byte[valueBytes.Length];
                        decryptedByteCount = reader.Read(decrypted, 0, decrypted.Length);
                    }
                }
                catch (Exception ex)
                {
                    return String.Empty;
                }

                cipher.Clear();
            }
            return Encoding.UTF8.GetString(decrypted, 0, decryptedByteCount);
        }

        public static byte[] Encrypt(string value, string password)
        {
            return Encrypt<AesManaged>(value, password);
        }

        public static byte[] Encrypt<T>(string value, string password) where T : SymmetricAlgorithm, new()
        {
            byte[] vectorBytes = Encoding.ASCII.GetBytes(_vector); // GetBytes<ASCIIEncoding>(_vector);
            byte[] saltBytes = Encoding.ASCII.GetBytes(_salt); // GetBytes<ASCIIEncoding>(_salt);
            byte[] valueBytes = Encoding.UTF8.GetBytes(value); // GetBytes<UTF8Encoding>(value);

            byte[] encrypted;
            using (T cipher = new T())
            {
                PasswordDeriveBytes _passwordBytes =
                    new PasswordDeriveBytes(password, saltBytes, _hash, _iterations);
                byte[] keyBytes = _passwordBytes.GetBytes(_keySize / 8);

                cipher.Mode = CipherMode.CBC;

                using (ICryptoTransform encryptor = cipher.CreateEncryptor(keyBytes, vectorBytes))
                {
                    using MemoryStream to = new MemoryStream();
                    using CryptoStream writer = new CryptoStream(to, encryptor, CryptoStreamMode.Write);

                    writer.Write(valueBytes, 0, valueBytes.Length);
                    writer.FlushFinalBlock();
                    encrypted = to.ToArray();
                }
                cipher.Clear();
            }
            return encrypted;
        }
    }
}