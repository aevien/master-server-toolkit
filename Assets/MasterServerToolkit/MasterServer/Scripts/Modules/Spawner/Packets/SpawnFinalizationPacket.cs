using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnFinalizationPacket : SerializablePacket
    {
        public int SpawnTaskId { get; set; }
        public MstProperties FinalizationData { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(SpawnTaskId);
            writer.Write(FinalizationData.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            SpawnTaskId = reader.ReadInt32();
            FinalizationData = new MstProperties(reader.ReadDictionary());
        }
    }
}