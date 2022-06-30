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
            var splitted = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            _value = splitted.ToList();
        }

        public override string Serialize()
        {
            return string.Join(",", _value);
        }

        protected override string ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadString();
        }

        protected override void WriteValue(string value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }
    }
}
