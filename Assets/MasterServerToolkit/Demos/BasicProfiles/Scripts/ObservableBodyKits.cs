using MasterServerToolkit.Dev;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit
{
    public class ObservableBodyKits : ObservableBaseDictionary<string, BodyKits>
    {
        public ObservableBodyKits(ushort key) : base(key) { }

        public override void Deserialize(string value) { }

        public override string Serialize()
        {
            return _value.ToString();
        }

        protected override string ReadKey(EndianBinaryReader reader)
        {
            return reader.ReadString();
        }

        protected override BodyKits ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadPacket(new BodyKits());
        }

        protected override void WriteKey(string key, EndianBinaryWriter writer)
        {
            writer.Write(key);
        }

        protected override void WriteValue(BodyKits value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }
    }
}