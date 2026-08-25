using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Reads MST4/MST5 pre-v2 authentication tokens during the release transition.
    /// New data must never be written with this format outside migration tests.
    /// </summary>
    internal static class MstLegacyTokenCrypto
    {
        private static readonly byte[] Salt = Encoding.ASCII.GetBytes("o6806642kbM7c5");

        public static string Encrypt(string plainText, string sharedSecret)
        {
            if (plainText == null)
                throw new ArgumentNullException(nameof(plainText));

            using var key = new Rfc2898DeriveBytes(sharedSecret, Salt);
            using var aes = new RijndaelManaged();
            aes.Key = key.GetBytes(aes.KeySize / 8);

            using var stream = new MemoryStream();
            stream.Write(BitConverter.GetBytes(aes.IV.Length), 0, sizeof(int));
            stream.Write(aes.IV, 0, aes.IV.Length);

            using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            using (var cryptoStream = new CryptoStream(
                       stream,
                       encryptor,
                       CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cryptoStream))
            {
                writer.Write(plainText);
            }

            return Convert.ToBase64String(stream.ToArray());
        }

        public static string Decrypt(string cipherText, string sharedSecret)
        {
            if (string.IsNullOrEmpty(cipherText))
                throw new ArgumentException("Cipher text is required", nameof(cipherText));

            if (cipherText.Length > Networking.MstNetworkLimits.MaxTokenCipherTextCharacterCount)
            {
                throw new InvalidDataException(
                    $"Cipher text length {cipherText.Length} exceeds the allowed limit " +
                    $"{Networking.MstNetworkLimits.MaxTokenCipherTextCharacterCount}");
            }

            byte[] encryptedBytes = Convert.FromBase64String(cipherText);
            using var key = new Rfc2898DeriveBytes(sharedSecret, Salt);
            using var stream = new MemoryStream(encryptedBytes);
            using var aes = new RijndaelManaged();
            aes.Key = key.GetBytes(aes.KeySize / 8);
            aes.IV = ReadByteArray(stream, aes.BlockSize / 8);

            using ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var cryptoStream = new CryptoStream(
                stream,
                decryptor,
                CryptoStreamMode.Read);
            using var reader = new StreamReader(cryptoStream);
            return reader.ReadToEnd();
        }

        private static byte[] ReadByteArray(Stream stream, int expectedLength)
        {
            var rawLength = new byte[sizeof(int)];

            if (stream.Read(rawLength, 0, rawLength.Length) != rawLength.Length)
                throw new InvalidDataException("Legacy token IV length is truncated");

            int length = BitConverter.ToInt32(rawLength, 0);

            if (length != expectedLength)
            {
                throw new InvalidDataException(
                    $"Legacy token IV length {length} does not match {expectedLength}");
            }

            var buffer = new byte[length];

            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                throw new InvalidDataException("Legacy token IV is truncated");

            return buffer;
        }
    }
}
