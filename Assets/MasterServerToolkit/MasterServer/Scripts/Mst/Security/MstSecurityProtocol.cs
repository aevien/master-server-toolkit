using System;
using System.IO;
using System.Text;

namespace MasterServerToolkit.MasterServer
{
    internal static class MstSecurityPurposes
    {
        public const string AuthSignIn = "auth.sign-in";
        public const string AuthSignUp = "auth.sign-up";
        public const string AuthenticationToken = "auth.token";
    }

    internal static class MstSecurityProtocol
    {
        public const byte CurrentVersion = 2;
        public const byte Aes256GcmRsaOaepSha256 = 1;
        public const int DataKeySize = 32;
        public const int NonceSize = 12;
        public const int AuthenticationTagSize = 16;
        public const int ChallengeIdSize = 16;
        public const int ChallengeNonceSize = 32;
        public const int MaximumKeyIdByteCount = 128;
        public const int MaximumPurposeByteCount = 256;
        public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromSeconds(30);

        public static byte[] CreateAssociatedData(
            string purpose,
            ushort targetOpCode,
            string keyId,
            byte[] challengeId,
            byte[] challengeNonce)
        {
            ValidatePurpose(purpose);
            ValidateKeyId(keyId);

            if (challengeId == null || challengeId.Length != ChallengeIdSize)
                throw new InvalidDataException($"Challenge id must contain {ChallengeIdSize} bytes");

            if (challengeNonce == null || challengeNonce.Length != ChallengeNonceSize)
            {
                throw new InvalidDataException(
                    $"Challenge nonce must contain {ChallengeNonceSize} bytes");
            }

            byte[] purposeBytes = Encoding.UTF8.GetBytes(purpose);
            byte[] keyIdBytes = Encoding.UTF8.GetBytes(keyId);

            using (var stream = new MemoryStream())
            using (var writer = new Networking.EndianBinaryWriter(
                       Networking.EndianBitConverter.Big,
                       stream))
            {
                writer.Write(CurrentVersion);
                writer.Write(Aes256GcmRsaOaepSha256);
                writer.Write(targetOpCode);
                writer.Write((ushort)purposeBytes.Length);
                writer.Write(purposeBytes);
                writer.Write((ushort)keyIdBytes.Length);
                writer.Write(keyIdBytes);
                writer.Write(challengeId);
                writer.Write(challengeNonce);
                return stream.ToArray();
            }
        }

        public static byte[] CreateDataAssociatedData(string purpose, string keyId)
        {
            ValidatePurpose(purpose);
            ValidateKeyId(keyId);

            return Encoding.UTF8.GetBytes(
                $"mst-security|v{CurrentVersion}|data|{keyId}|{purpose}");
        }

        public static void ValidatePurpose(string purpose)
        {
            if (string.IsNullOrWhiteSpace(purpose))
                throw new ArgumentException("Encryption purpose is required", nameof(purpose));

            int byteCount = Encoding.UTF8.GetByteCount(purpose);

            if (byteCount > MaximumPurposeByteCount)
            {
                throw new InvalidDataException(
                    $"Encryption purpose exceeds {MaximumPurposeByteCount} UTF-8 bytes");
            }
        }

        public static void ValidateKeyId(string keyId)
        {
            if (string.IsNullOrWhiteSpace(keyId))
                throw new InvalidDataException("Encryption key id is required");

            int byteCount = Encoding.UTF8.GetByteCount(keyId);

            if (byteCount > MaximumKeyIdByteCount)
                throw new InvalidDataException($"Encryption key id exceeds {MaximumKeyIdByteCount} UTF-8 bytes");
        }

        public static string ToBase64Url(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return Convert.ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public static bool TryFromBase64Url(string value, out byte[] bytes)
        {
            bytes = null;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string base64 = value.Replace('-', '+').Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 0:
                    break;
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
                default:
                    return false;
            }

            try
            {
                bytes = Convert.FromBase64String(base64);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
