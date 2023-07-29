using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableListString : ObservableBaseList<string>
    {
        public ObservableListString(ushort key) : base(key) { }

        public ObservableListString(ushort key, List<string> defaultValues) : base(key, defaultValues) { }

        public override void Deserialize(string value)
        {
            var splitted = value.Split(";", StringSplitOptions.RemoveEmptyEntries);
            _value = splitted.ToList();
        }

        public override string Serialize()
        {
            return string.Join(";", _value);
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
                _value.Add(v.StringValue);
            }
        }

        protected override string ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadString();
        }

        protected override void WriteValue(string value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void FromJson(string json)
        {
            FromJson(new MstJson(json));
        }
    }
}
