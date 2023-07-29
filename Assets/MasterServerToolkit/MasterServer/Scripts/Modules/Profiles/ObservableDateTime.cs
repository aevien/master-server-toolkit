using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableDateTime : ObservableBase<DateTime>
    {
        public ObservableDateTime(ushort key) : base(key, DateTime.UtcNow) { }
        public ObservableDateTime(ushort key, DateTime value) : base(key, value) { }

        public override byte[] GetUpdates()
        {
            return ToBytes();
        }

        public override void ApplyUpdates(byte[] data)
        {
            FromBytes(data);
        }

        public override void ClearUpdates() { }

        public override string Serialize()
        {
            return _value.ToString();
        }

        public override void Deserialize(string value)
        {
            DateTime.TryParse(value, out _value);
        }

        public override void FromBytes(byte[] data)
        {
            long binary = EndianBitConverter.Big.ToInt64(data, 0);
            _value = DateTime.FromBinary(binary);
            MarkDirty();
        }

        public override byte[] ToBytes()
        {
            var data = new byte[sizeof(long)];
            EndianBitConverter.Big.CopyBytes(_value.ToBinary(), data, 0);
            return data;
        }

        public override MstJson ToJson()
        {
            return MstJson.Create(_value.ToString());
        }

        public override void FromJson(MstJson json)
        {
            DateTime.TryParse(json.StringValue, out _value);
        }

        public override void FromJson(string json)
        {
            FromJson(MstJson.Create(json));
        }
    }
}