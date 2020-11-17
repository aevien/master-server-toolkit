using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictionaryString : ObservableBaseDictionary<string, string>
    {
        public ObservableDictionaryString(short key) : base(key) { }
        public ObservableDictionaryString(short key, Dictionary<string, string> defaultValues) : base(key, defaultValues) { }

        public override string Serialize()
        {
            IEnumerable<string> propertyStringArray = _value.Select(i => i.Key + ":" + i.Value.ToString());
            return propertyStringArray != null ? string.Join(",", propertyStringArray) : string.Empty;
        }

        public override void Deserialize(string value)
        {
            string[] kvpArray = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            _value = kvpArray.Select(i =>
            {
                return i.Split(':');
            }).ToDictionary(k => k[0], v => v[1]);
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