using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class RegisterSpawnedProcessPacket : SerializablePacket
    {
        public int SpawnId { get; set; }
        public string SpawnCode { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(SpawnId);
            writer.Write(SpawnCode);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            SpawnId = reader.ReadInt32();
            SpawnCode = reader.ReadString();
        }
    }
}