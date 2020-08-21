using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnedProcessStartedPacket : SerializablePacket
    {
        public int SpawnId { get; set; }
        public int ProcessId { get; set; }
        public string CmdArgs { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(SpawnId);
            writer.Write(ProcessId);
            writer.Write(CmdArgs);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            SpawnId = reader.ReadInt32();
            ProcessId = reader.ReadInt32();
            CmdArgs = reader.ReadString();
        }
    }
}