using MasterServerToolkit.Networking;
using System;
using System.IO;

namespace MasterServerToolkit.MasterServer
{
    internal sealed class MstProtectedDataPacket : SerializablePacket
    {
        public byte Version { get; set; } = MstSecurityProtocol.CurrentVersion;
        public byte Algorithm { get; set; } = MstSecurityProtocol.Aes256GcmRsaOaepSha256;
        public string KeyId { get; set; }
        public byte[] Nonce { get; set; }
        public byte[] CipherText { get; set; }
        public byte[] AuthenticationTag { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            Validate();
            writer.Write(Version);
            writer.Write(Algorithm);
            writer.Write(KeyId);
            writer.Write(Nonce);
            writer.Write(CipherText.Length);
            writer.Write(CipherText);
            writer.Write(AuthenticationTag);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Version = reader.ReadByte();
            Algorithm = reader.ReadByte();
            KeyId = reader.ReadString(MstSecurityProtocol.MaximumKeyIdByteCount);
            Nonce = reader.ReadBytesOrThrow(MstSecurityProtocol.NonceSize);
            int cipherTextLength = reader.ReadLength32(
                MstNetworkLimits.MaxAuthenticationPlaintextByteCount,
                "Protected data ciphertext");
            CipherText = reader.ReadBytesExact(
                cipherTextLength,
                MstNetworkLimits.MaxAuthenticationPlaintextByteCount);
            AuthenticationTag =
                reader.ReadBytesOrThrow(MstSecurityProtocol.AuthenticationTagSize);
            Validate();
        }

        private void Validate()
        {
            if (Version != MstSecurityProtocol.CurrentVersion)
                throw new InvalidDataException($"Unsupported encrypted data version {Version}");

            if (Algorithm != MstSecurityProtocol.Aes256GcmRsaOaepSha256)
                throw new InvalidDataException($"Unsupported encrypted data algorithm {Algorithm}");

            MstSecurityProtocol.ValidateKeyId(KeyId);

            if (Nonce == null || Nonce.Length != MstSecurityProtocol.NonceSize)
                throw new InvalidDataException("Invalid AES-GCM nonce");

            if (CipherText == null ||
                CipherText.Length > MstNetworkLimits.MaxAuthenticationPlaintextByteCount)
            {
                throw new InvalidDataException("Invalid encrypted data ciphertext");
            }

            if (AuthenticationTag == null ||
                AuthenticationTag.Length != MstSecurityProtocol.AuthenticationTagSize)
            {
                throw new InvalidDataException("Invalid AES-GCM authentication tag");
            }
        }
    }
}
