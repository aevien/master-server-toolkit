using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
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

        public override MstJson ToJson()
        {
            var json = MstJson.EmptyObject;

            foreach (var kvp in _value)
            {
                json.AddField(kvp.Key.ToString(), kvp.Value);
            }

            return json;
        }

        public override void FromJson(MstJson json)
        {
            _value.Clear();

            foreach (var key in json.Keys)
            {
                _value.TryAdd(Convert.ToInt32(key), json[key].IntValue);
            }
        }

        public override void FromJson(string json)
        {
            FromJson(new MstJson(json));
        }
    }
}