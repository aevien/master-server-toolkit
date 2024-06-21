using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System.Collections.Concurrent;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictStringFloat : ObservableBaseDictionary<string, float>
    {
        public ObservableDictStringFloat(ushort key) : base(key) { }
        public ObservableDictStringFloat(ushort key, ConcurrentDictionary<string, float> defaultValues) : base(key, defaultValues) { }

        public override string Serialize()
        {
            return string.Empty;
        }

        public override void Deserialize(string value) { }

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

        public override MstJson ToJson()
        {
            var json = MstJson.EmptyObject;

            foreach (var kvp in _value)
            {
                json.AddField(kvp.Key, kvp.Value);
            }

            return json;
        }

        public override void FromJson(MstJson json)
        {
            _value.Clear();

            foreach (var key in json.Keys)
            {
                _value.TryAdd(key, json[key].FloatValue);
            }
        }

        public override void FromJson(string json)
        {
            FromJson(new MstJson(json));
        }
    }
}