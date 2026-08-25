using MasterServerToolkit.Networking;
using System.IO;

namespace MasterServerToolkit.MasterServer
{
    public sealed class MstSealChallengeRequestPacket : SerializablePacket
    {
        public byte Version { get; set; } = MstSecurityProtocol.CurrentVersion;
        public ushort TargetOpCode { get; set; }
        public string Purpose { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            Validate();
            writer.Write(Version);
            writer.Write(TargetOpCode);
            writer.Write(Purpose);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Version = reader.ReadByte();
            TargetOpCode = reader.ReadUInt16();
            Purpose = reader.ReadString(MstSecurityProtocol.MaximumPurposeByteCount);
            Validate();
        }

        private void Validate()
        {
            if (Version != MstSecurityProtocol.CurrentVersion)
                throw new InvalidDataException($"Unsupported seal request version {Version}");

            if (TargetOpCode == 0)
                throw new InvalidDataException("Target opcode is required");

            MstSecurityProtocol.ValidatePurpose(Purpose);
        }
    }
}
