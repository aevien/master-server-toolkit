using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnRequestPacket : SerializablePacket
    {
        public int SpawnerId { get; set; }
        public int SpawnTaskId { get; set; }
        public string SpawnTaskUniqueCode { get; set; } = string.Empty;
        public string OverrideExePath { get; set; } = string.Empty;
        public MstProperties Options { get; set; }
        public bool UseOverrideExePath => !string.IsNullOrEmpty(OverrideExePath);

        public SpawnRequestPacket()
        {
            Options = new MstProperties();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(SpawnerId);
            writer.Write(SpawnTaskId);
            writer.Write(SpawnTaskUniqueCode);
            writer.Write(OverrideExePath);
            writer.Write(Options.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            SpawnerId = reader.ReadInt32();
            SpawnTaskId = reader.ReadInt32();
            SpawnTaskUniqueCode = reader.ReadString();
            OverrideExePath = reader.ReadString();
            Options = new MstProperties(reader.ReadDictionary());
        }
    }
}