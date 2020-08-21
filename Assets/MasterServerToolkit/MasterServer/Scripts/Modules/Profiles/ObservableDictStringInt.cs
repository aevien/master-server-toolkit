using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictStringInt : ObservableBaseDictionary<string, int>
    {
        public ObservableDictStringInt(short key) : base(key) { }
        public ObservableDictStringInt(short key, Dictionary<string, int> defaultValues) : base(key, defaultValues) { }

        public override string Serialize()
        {
            return JsonConvert.SerializeObject(_value);
        }

        public override void Deserialize(string value)
        {
            _value = JsonConvert.DeserializeObject<Dictionary<string, int>>(value);
        }

        protected override string ReadKey(EndianBinaryReader reader)
        {
            return reader.ReadString();
        }

        protected override int ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void WriteKey(string key, EndianBinaryWriter writer)
        {
            writer.Write(key);
        }

        protected override void WriteValue(int value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }
    }
}