using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ModuleInfoPacket : SerializablePacket
    {
        public string Id { get; set; }
        public string Module { get; set; }
        public MstJson Data { get; set; }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Module = reader.ReadString();
            Data = new MstJson(reader.ReadString());
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Module);
            writer.Write(Data.ToString());
        }
    }
}