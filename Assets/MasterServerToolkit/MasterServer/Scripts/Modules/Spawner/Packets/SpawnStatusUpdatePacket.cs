using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnStatusUpdatePacket : SerializablePacket
    {
        public int SpawnId { get; set; }
        public SpawnStatus Status { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(SpawnId);
            writer.Write((int)Status);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            SpawnId = reader.ReadInt32();
            Status = (SpawnStatus)reader.ReadInt32();
        }
    }
}