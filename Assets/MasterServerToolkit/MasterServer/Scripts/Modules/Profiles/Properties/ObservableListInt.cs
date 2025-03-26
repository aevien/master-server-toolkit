using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableListInt : ObservableBaseList<int>
    {
        public ObservableListInt(ushort key) : base(key) { }

        public ObservableListInt(ushort key, List<int> defaultValues) : base(key, defaultValues) { }

        public override void Deserialize(string value)
        {
            FromJson(value);
        }

        public override string Serialize()
        {
            return ToJson().ToString();
        }

        public override MstJson ToJson()
        {
            var json = MstJson.EmptyArray;

            foreach (var v in _value)
            {
                json.Add(v);
            }

            return json;
        }

        public override void FromJson(MstJson json)
        {
            _value.Clear();

            foreach (var v in json)
            {
                _value.Add(v.IntValue);
            }

            MarkAsDirty();
        }

        protected override int ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void WriteValue(int value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void FromJson(string json)
        {
            FromJson(new MstJson(json));
        }
    }
}
