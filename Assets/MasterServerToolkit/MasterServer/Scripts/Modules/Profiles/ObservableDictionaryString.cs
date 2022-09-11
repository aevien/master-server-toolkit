using MasterServerToolkit.Networking;
using System.Collections.Concurrent;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictionaryString : ObservableBaseDictionary<string, string>
    {
        public ObservableDictionaryString(ushort key) : base(key) { }
        public ObservableDictionaryString(ushort key, ConcurrentDictionary<string, string> defaultValues) : base(key, defaultValues) { }

        public override string Serialize()
        {
            return string.Empty;
        }

        public override void Deserialize(string value) { }

        protected override void WriteValue(string value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }

        protected override string ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadString();
        }

        protected override void WriteKey(string key, EndianBinaryWriter writer)
        {
            writer.Write(key);
        }

        protected override string ReadKey(EndianBinaryReader reader)
        {
            return reader.ReadString();
        }
    }
}