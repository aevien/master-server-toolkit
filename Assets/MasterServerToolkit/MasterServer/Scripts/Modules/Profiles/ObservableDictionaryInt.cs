using MasterServerToolkit.Networking;
using System.Collections.Concurrent;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictionaryInt : ObservableBaseDictionary<int, int>
    {
        public ObservableDictionaryInt(ushort key) : base(key) { }

        public ObservableDictionaryInt(ushort key, ConcurrentDictionary<int, int> defaultValues) : base(key, defaultValues) { }

        public override string Serialize()
        {
            return string.Empty;
        }

        public override void Deserialize(string value) { }

        protected override void WriteValue(int value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }

        protected override int ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void WriteKey(int key, EndianBinaryWriter writer)
        {
            writer.Write(key);
        }

        protected override int ReadKey(EndianBinaryReader reader)
        {
            return reader.ReadInt32();
        }
    }
}