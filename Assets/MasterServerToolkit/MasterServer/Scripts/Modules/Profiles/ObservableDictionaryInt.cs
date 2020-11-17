using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictionaryInt : ObservableBaseDictionary<int, int>
    {
        public ObservableDictionaryInt(short key) : base(key) { }

        public ObservableDictionaryInt(short key, Dictionary<int, int> defaultValues) : base(key, defaultValues)  { }

        public override string Serialize()
        {
            IEnumerable<string> propertyStringArray = _value.Select(i => i.Key + ":" + i.Value.ToString());
            return propertyStringArray != null ? string.Join(",", propertyStringArray) : "0";
        }

        public override void Deserialize(string value)
        {
            string[] kvpArray = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            _value = kvpArray.Select(i =>
            {
                return i.Split(':');
            }).ToDictionary(k => Convert.ToInt32(k[0]), v => Convert.ToInt32(v[1]));
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