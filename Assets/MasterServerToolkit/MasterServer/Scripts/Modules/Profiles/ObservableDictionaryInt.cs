using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictionaryInt : ObservableBaseDictionary<int, int>
    {
        public ObservableDictionaryInt(short key) : base(key) { }

        public ObservableDictionaryInt(short key, Dictionary<int, int> defaultValues) : base(key, defaultValues)  { }

        public override string Serialize()
        {
            return JsonConvert.SerializeObject(_value);
        }

        public override void Deserialize(string value)
        {
            _value = JsonConvert.DeserializeObject<Dictionary<int, int>>(value);
        }

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