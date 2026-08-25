using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ProvideServerAccessCheckPacket : SerializablePacket
    {
        public const byte CurrentVersion = 2;
        public const int ChallengeIdSize = 16;
        public const int ProofSize = 32;

        public byte Version { get; set; } = CurrentVersion;
        public byte[] ChallengeId { get; set; } = new byte[ChallengeIdSize];
        public byte[] Proof { get; set; } = new byte[ProofSize];

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(ChallengeId);
            writer.Write(Proof);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Version = reader.ReadByte();
            ChallengeId = reader.ReadBytesOrThrow(ChallengeIdSize);
            Proof = reader.ReadBytesOrThrow(ProofSize);
        }
    }
}
