using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableListFloat : ObservableBaseList<float>
    {
        public ObservableListFloat(ushort key) : base(key) { }

        public ObservableListFloat(ushort key, List<float> defaultValues) : base(key, defaultValues) { }

        public override void Deserialize(string value)
        {
            var splitted = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            _value = new List<float>();

            foreach (string i in splitted)
            {
                try
                {
                    _value.Add(Convert.ToSingle(i));
                }
                catch
                {
                    continue;
                }
            }
        }

        public override string Serialize()
        {
            return string.Join(",", _value);
        }

        protected override float ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadSingle();
        }

        protected override void WriteValue(float value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }
    }
}
