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
            var splitted = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            _value = new List<int>();

            foreach (string i in splitted)
            {
                try
                {
                    _value.Add(Convert.ToInt32(i));
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

        protected override int ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void WriteValue(int value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }
    }
}
