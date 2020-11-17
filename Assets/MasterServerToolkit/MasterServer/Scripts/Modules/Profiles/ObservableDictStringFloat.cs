using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDictStringFloat : ObservableBaseDictionary<string, float>
    {
        public ObservableDictStringFloat(short key) : base(key) { }
        public ObservableDictStringFloat(short key, Dictionary<string, float> defaultValues) : base(key, defaultValues) { }

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
            }).ToDictionary(k => k[0], v => Convert.ToSingle(v[1]));
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