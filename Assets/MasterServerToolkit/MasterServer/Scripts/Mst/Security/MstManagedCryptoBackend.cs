#if !UNITY_WEBGL || UNITY_EDITOR
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;

namespace MasterServerToolkit.MasterServer
{
    internal static class MstManagedCryptoBackend
    {
        private const int RsaKeySize = 2048;
        private const int RsaCertainty = 100;
        private const int GcmTagBitCount = MstSecurityProtocol.AuthenticationTagSize * 8;

        private static readonly SecureRandom secureRandom = new SecureRandom();

        public static byte[] RandomBytes(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            var bytes = new byte[length];
            secureRandom.NextBytes(bytes);
            return bytes;
        }

        public static byte[] DeriveKey(byte[] sourceKey, string purpose)
        {
            if (sourceKey == null || sourceKey.Length != MstSecurityProtocol.DataKeySize)
                throw new ArgumentException("Source key must contain 32 bytes", nameof(sourceKey));

            MstSecurityProtocol.ValidatePurpose(purpose);

            var generator = new HkdfBytesGenerator(new Sha256Digest());
            generator.Init(new HkdfParameters(
                sourceKey,
                System.Text.Encoding.UTF8.GetBytes("mst-security-v2"),
                System.Text.Encoding.UTF8.GetBytes(purpose)));

            var key = new byte[MstSecurityProtocol.DataKeySize];
            generator.GenerateBytes(key, 0, key.Length);
            return key;
        }

        public static byte[] EncryptAead(
            byte[] key,
            byte[] nonce,
            byte[] plaintext,
            byte[] associatedData,
            out byte[] authenticationTag)
        {
            ValidateAeadArguments(key, nonce, plaintext, associatedData);

            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(true, new AeadParameters(
                new KeyParameter(key),
                GcmTagBitCount,
                nonce,
                associatedData));

            var combined = new byte[cipher.GetOutputSize(plaintext.Length)];
            int length = cipher.ProcessBytes(plaintext, 0, plaintext.Length, combined, 0);
            length += cipher.DoFinal(combined, length);

            int cipherTextLength = length - MstSecurityProtocol.AuthenticationTagSize;
            var cipherText = new byte[cipherTextLength];
            authenticationTag = new byte[MstSecurityProtocol.AuthenticationTagSize];
            Buffer.BlockCopy(combined, 0, cipherText, 0, cipherText.Length);
            Buffer.BlockCopy(
                combined,
                cipherText.Length,
                authenticationTag,
                0,
                authenticationTag.Length);
            Array.Clear(combined, 0, combined.Length);
            return cipherText;
        }

        public static bool TryDecryptAead(
            byte[] key,
            byte[] nonce,
            byte[] cipherText,
            byte[] authenticationTag,
            byte[] associatedData,
            out byte[] plaintext)
        {
            plaintext = null;

            try
            {
                ValidateAeadArguments(key, nonce, cipherText, associatedData);

                if (authenticationTag == null ||
                    authenticationTag.Length != MstSecurityProtocol.AuthenticationTagSize)
                {
                    return false;
                }

                var combined = new byte[cipherText.Length + authenticationTag.Length];
                Buffer.BlockCopy(cipherText, 0, combined, 0, cipherText.Length);
                Buffer.BlockCopy(
                    authenticationTag,
                    0,
                    combined,
                    cipherText.Length,
                    authenticationTag.Length);

                try
                {
                    var cipher = new GcmBlockCipher(new AesEngine());
                    cipher.Init(false, new AeadParameters(
                        new KeyParameter(key),
                        GcmTagBitCount,
                        nonce,
                        associatedData));

                    var output = new byte[cipher.GetOutputSize(combined.Length)];
                    int length = cipher.ProcessBytes(combined, 0, combined.Length, output, 0);
                    length += cipher.DoFinal(output, length);

                    if (length == output.Length)
                    {
                        plaintext = output;
                    }
                    else
                    {
                        plaintext = new byte[length];
                        Buffer.BlockCopy(output, 0, plaintext, 0, length);
                        Array.Clear(output, 0, output.Length);
                    }

                    return true;
                }
                finally
                {
                    Array.Clear(combined, 0, combined.Length);
                }
            }
            catch (InvalidCipherTextException)
            {
                plaintext = null;
                return false;
            }
            catch (ArgumentException)
            {
                plaintext = null;
                return false;
            }
        }

        public static byte[] WrapKey(byte[] publicKeySpki, byte[] dataKey)
        {
            if (publicKeySpki == null || publicKeySpki.Length == 0)
                throw new ArgumentException("Public key is required", nameof(publicKeySpki));

            if (dataKey == null || dataKey.Length != MstSecurityProtocol.DataKeySize)
                throw new ArgumentException("Data key must contain 32 bytes", nameof(dataKey));

            AsymmetricKeyParameter publicKey = PublicKeyFactory.CreateKey(publicKeySpki);
            var cipher = CreateRsaOaepCipher();
            cipher.Init(true, publicKey);
            return cipher.ProcessBlock(dataKey, 0, dataKey.Length);
        }

        public static bool TryUnwrapKey(
            byte[] privateKeyPkcs8,
            byte[] encryptedDataKey,
            out byte[] dataKey)
        {
            dataKey = null;

            try
            {
                if (privateKeyPkcs8 == null || privateKeyPkcs8.Length == 0 ||
                    encryptedDataKey == null || encryptedDataKey.Length == 0)
                {
                    return false;
                }

                AsymmetricKeyParameter privateKey = PrivateKeyFactory.CreateKey(privateKeyPkcs8);
                var cipher = CreateRsaOaepCipher();
                cipher.Init(false, privateKey);
                byte[] unwrapped = cipher.ProcessBlock(
                    encryptedDataKey,
                    0,
                    encryptedDataKey.Length);

                if (unwrapped.Length != MstSecurityProtocol.DataKeySize)
                {
                    Array.Clear(unwrapped, 0, unwrapped.Length);
                    return false;
                }

                dataKey = unwrapped;
                return true;
            }
            catch (Exception)
            {
                dataKey = null;
                return false;
            }
        }

        public static void GenerateRsaKeyPair(
            out byte[] publicKeySpki,
            out byte[] privateKeyPkcs8)
        {
            var generator = new RsaKeyPairGenerator();
            generator.Init(new RsaKeyGenerationParameters(
                BigInteger.ValueOf(65537),
                secureRandom,
                RsaKeySize,
                RsaCertainty));

            AsymmetricCipherKeyPair keyPair = generator.GenerateKeyPair();
            SubjectPublicKeyInfo publicInfo =
                SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public);
            PrivateKeyInfo privateInfo =
                PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private);
            publicKeySpki = publicInfo.GetEncoded();
            privateKeyPkcs8 = privateInfo.GetEncoded();
        }

        public static string CreateKeyId(byte[] keyMaterial, string prefix)
        {
            if (keyMaterial == null || keyMaterial.Length == 0)
                throw new ArgumentException("Key material is required", nameof(keyMaterial));

            var digest = new Sha256Digest();
            digest.BlockUpdate(keyMaterial, 0, keyMaterial.Length);
            var hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);
            var shortHash = new byte[12];
            Buffer.BlockCopy(hash, 0, shortHash, 0, shortHash.Length);
            Array.Clear(hash, 0, hash.Length);
            return $"{prefix}-{MstSecurityProtocol.ToBase64Url(shortHash)}";
        }

        private static OaepEncoding CreateRsaOaepCipher()
        {
            return new OaepEncoding(
                new RsaEngine(),
                new Sha256Digest(),
                new Sha256Digest(),
                Array.Empty<byte>());
        }

        private static void ValidateAeadArguments(
            byte[] key,
            byte[] nonce,
            byte[] data,
            byte[] associatedData)
        {
            if (key == null || key.Length != MstSecurityProtocol.DataKeySize)
                throw new ArgumentException("AES key must contain 32 bytes", nameof(key));

            if (nonce == null || nonce.Length != MstSecurityProtocol.NonceSize)
                throw new ArgumentException("AES-GCM nonce must contain 12 bytes", nameof(nonce));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (associatedData == null)
                throw new ArgumentNullException(nameof(associatedData));
        }
    }
}
#endif
