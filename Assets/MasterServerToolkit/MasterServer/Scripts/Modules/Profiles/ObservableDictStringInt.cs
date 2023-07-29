using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System.Collections.Concurrent;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictStringInt : ObservableBaseDictionary<string, int>
    {
        public ObservableDictStringInt(ushort key) : base(key) { }
        public ObservableDictStringInt(ushort key, ConcurrentDictionary<string, int> defaultValues) : base(key, defaultValues) { }

        public override string Serialize()
        {
            return new MstProperties().Append(_value).ToReadableString();
        }

        public override void Deserialize(string value)
        {
            var properties = new MstProperties();
            properties.FromReadableString(value);

            foreach (var property in properties)
            {
                _value[property.Key] = properties.AsInt(property.Key);
            }
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
                _value.TryAdd(key, json[key].IntValue);
            }
        }

        public override void FromJson(string json)
        {
            FromJson(new MstJson(json));
        }
    }
}