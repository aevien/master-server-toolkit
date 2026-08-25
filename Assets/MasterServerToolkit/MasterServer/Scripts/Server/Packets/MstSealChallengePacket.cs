using MasterServerToolkit.Networking;
using System.IO;

namespace MasterServerToolkit.MasterServer
{
    public sealed class MstSealChallengePacket : SerializablePacket
    {
        public byte Version { get; set; } = MstSecurityProtocol.CurrentVersion;
        public byte Algorithm { get; set; } = MstSecurityProtocol.Aes256GcmRsaOaepSha256;
        public string KeyId { get; set; }
        public byte[] PublicKeySpki { get; set; }
        public byte[] ChallengeId { get; set; }
        public byte[] ChallengeNonce { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            Validate();
            writer.Write(Version);
            writer.Write(Algorithm);
            writer.Write(KeyId);
            writer.Write((ushort)PublicKeySpki.Length);
            writer.Write(PublicKeySpki);
            writer.Write(ChallengeId);
            writer.Write(ChallengeNonce);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Version = reader.ReadByte();
            Algorithm = reader.ReadByte();
            KeyId = reader.ReadString(MstSecurityProtocol.MaximumKeyIdByteCount);
            int publicKeyLength = reader.ReadUInt16();
            PublicKeySpki = reader.ReadBytesExact(
                publicKeyLength,
                MstNetworkLimits.MaxSecurityPublicKeyByteCount);
            ChallengeId = reader.ReadBytesOrThrow(MstSecurityProtocol.ChallengeIdSize);
            ChallengeNonce = reader.ReadBytesOrThrow(MstSecurityProtocol.ChallengeNonceSize);
            Validate();
        }

        private void Validate()
        {
            if (Version != MstSecurityProtocol.CurrentVersion)
                throw new InvalidDataException($"Unsupported seal challenge version {Version}");

            if (Algorithm != MstSecurityProtocol.Aes256GcmRsaOaepSha256)
                throw new InvalidDataException($"Unsupported seal challenge algorithm {Algorithm}");

            MstSecurityProtocol.ValidateKeyId(KeyId);

            if (PublicKeySpki == null || PublicKeySpki.Length == 0 ||
                PublicKeySpki.Length > MstNetworkLimits.MaxSecurityPublicKeyByteCount)
            {
                throw new InvalidDataException("Invalid master public key");
            }

            if (ChallengeId == null ||
                ChallengeId.Length != MstSecurityProtocol.ChallengeIdSize)
            {
                throw new InvalidDataException("Invalid seal challenge id");
            }

            if (ChallengeNonce == null ||
                ChallengeNonce.Length != MstSecurityProtocol.ChallengeNonceSize)
            {
                throw new InvalidDataException("Invalid seal challenge nonce");
            }
        }
    }
}
