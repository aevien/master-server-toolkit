using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ServerAccessChallengePacket : SerializablePacket
    {
        public const byte CurrentVersion = 2;
        public const int ChallengeIdSize = 16;
        public const int NonceSize = 32;

        public byte Version { get; set; } = CurrentVersion;
        public byte[] ChallengeId { get; set; } = new byte[ChallengeIdSize];
        public byte[] Nonce { get; set; } = new byte[NonceSize];
        public long ExpiresAtUtcTicks { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(ChallengeId);
            writer.Write(Nonce);
            writer.Write(ExpiresAtUtcTicks);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Version = reader.ReadByte();
            ChallengeId = reader.ReadBytesOrThrow(ChallengeIdSize);
            Nonce = reader.ReadBytesOrThrow(NonceSize);
            ExpiresAtUtcTicks = reader.ReadInt64();
        }
    }
}
