using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class ModuleInfoPacket : SerializablePacket
    {
        public string Id { get; set; }
        public string Module { get; set; }
        public JObject Data { get; set; }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Module = reader.ReadString();
            Data = JObject.Parse(reader.ReadString());
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Module);
            writer.Write(JsonConvert.SerializeObject(Data));
        }
    }
}