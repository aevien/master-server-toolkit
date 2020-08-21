using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictStringFloat : ObservableBaseDictionary<string, float>
    {
        public ObservableDictStringFloat(short key) : base(key) { }
        public ObservableDictStringFloat(short key, Dictionary<string, float> defaultValues) : base(key, defaultValues) { }

        public override string Serialize()
        {
            return JsonConvert.SerializeObject(_value);
        }

        public override void Deserialize(string value)
        {
            _value = JsonConvert.DeserializeObject<Dictionary<string, float>>(value);
        }

        protected override void WriteValue(float value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }

        protected override float ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadSingle();
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