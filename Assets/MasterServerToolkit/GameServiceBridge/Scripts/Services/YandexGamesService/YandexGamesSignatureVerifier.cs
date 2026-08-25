using MasterServerToolkit.MasterServer;
using System;
using System.Text;

namespace MasterServerToolkit.GameService
{
    public static class YandexGamesSignatureVerifier
    {
        public static bool TryVerify(string secret, string signatureValue, out string payloadJson)
        {
            payloadJson = null;

            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureValue))
                return false;

            string[] signature = signatureValue.Split('.');

            if (signature.Length != 2)
                return false;

            try
            {
                payloadJson = DecodeBase64String(signature[1]);
                string testSignature = Mst.Security.CreateSignatureHMAC_SHA256(secret, payloadJson);

                if (!Base64Equals(signature[0], testSignature))
                {
                    payloadJson = null;
                    return false;
                }

                return true;
            }
            catch
            {
                payloadJson = null;
                return false;
            }
        }

        private static bool Base64Equals(string first, string second)
        {
            byte[] firstBytes = DecodeBase64Bytes(first);
            byte[] secondBytes = DecodeBase64Bytes(second);

            if (firstBytes.Length != secondBytes.Length)
                return false;

            int diff = 0;

            for (int i = 0; i < firstBytes.Length; i++)
            {
                diff |= firstBytes[i] ^ secondBytes[i];
            }

            return diff == 0;
        }

        private static string DecodeBase64String(string value)
        {
            return Encoding.UTF8.GetString(DecodeBase64Bytes(value));
        }

        private static byte[] DecodeBase64Bytes(string value)
        {
            string normalized = value.Replace('-', '+').Replace('_', '/');

            switch (normalized.Length % 4)
            {
                case 0:
                    break;
                case 2:
                    normalized += "==";
                    break;
                case 3:
                    normalized += "=";
                    break;
                default:
                    throw new FormatException("Invalid Base64 value.");
            }

            return Convert.FromBase64String(normalized);
        }
    }
}
