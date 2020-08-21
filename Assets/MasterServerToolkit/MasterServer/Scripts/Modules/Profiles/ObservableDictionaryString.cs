using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictionaryString : ObservableBaseDictionary<string, string>
    {
        public ObservableDictionaryString(short key) : base(key) { }
        public ObservableDictionaryString(short key, Dictionary<string, string> defaultValues) : base(key, defaultValues) { }

        public override string Serialize()
        {
            return JsonConvert.SerializeObject(_value);
        }

        public override void Deserialize(string value)
        {
            _value = JsonConvert.DeserializeObject<Dictionary<string, string>>(value);
        }

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