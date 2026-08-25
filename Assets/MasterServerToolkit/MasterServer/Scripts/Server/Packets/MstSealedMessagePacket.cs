using MasterServerToolkit.Networking;
using System.IO;

namespace MasterServerToolkit.MasterServer
{
    public sealed class MstSealedMessagePacket : SerializablePacket
    {
        public byte Version { get; set; } = MstSecurityProtocol.CurrentVersion;
        public byte Algorithm { get; set; } = MstSecurityProtocol.Aes256GcmRsaOaepSha256;
        public string KeyId { get; set; }
        public byte[] ChallengeId { get; set; }
        public byte[] EncryptedDataKey { get; set; }
        public byte[] Nonce { get; set; }
        public byte[] CipherText { get; set; }
        public byte[] AuthenticationTag { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            Validate();
            writer.Write(Version);
            writer.Write(Algorithm);
            writer.Write(KeyId);
            writer.Write(ChallengeId);
            writer.Write((ushort)EncryptedDataKey.Length);
            writer.Write(EncryptedDataKey);
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
            ChallengeId = reader.ReadBytesOrThrow(MstSecurityProtocol.ChallengeIdSize);
            int encryptedDataKeyLength = reader.ReadUInt16();
            EncryptedDataKey = reader.ReadBytesExact(
                encryptedDataKeyLength,
                MstNetworkLimits.MaxSecurityWrappedKeyByteCount);
            Nonce = reader.ReadBytesOrThrow(MstSecurityProtocol.NonceSize);
            int cipherTextLength = reader.ReadLength32(
                MstNetworkLimits.MaxAuthenticationPlaintextByteCount,
                "Sealed message ciphertext");
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
                throw new InvalidDataException($"Unsupported sealed message version {Version}");

            if (Algorithm != MstSecurityProtocol.Aes256GcmRsaOaepSha256)
                throw new InvalidDataException($"Unsupported sealed message algorithm {Algorithm}");

            MstSecurityProtocol.ValidateKeyId(KeyId);

            if (ChallengeId == null ||
                ChallengeId.Length != MstSecurityProtocol.ChallengeIdSize)
            {
                throw new InvalidDataException("Invalid sealed message challenge id");
            }

            if (EncryptedDataKey == null || EncryptedDataKey.Length == 0 ||
                EncryptedDataKey.Length > MstNetworkLimits.MaxSecurityWrappedKeyByteCount)
            {
                throw new InvalidDataException("Invalid wrapped data key");
            }

            if (Nonce == null || Nonce.Length != MstSecurityProtocol.NonceSize)
                throw new InvalidDataException("Invalid sealed message nonce");

            if (CipherText == null ||
                CipherText.Length > MstNetworkLimits.MaxAuthenticationPlaintextByteCount)
            {
                throw new InvalidDataException("Invalid sealed message ciphertext");
            }

            if (AuthenticationTag == null ||
                AuthenticationTag.Length != MstSecurityProtocol.AuthenticationTagSize)
            {
                throw new InvalidDataException("Invalid sealed message authentication tag");
            }
        }
    }
}
