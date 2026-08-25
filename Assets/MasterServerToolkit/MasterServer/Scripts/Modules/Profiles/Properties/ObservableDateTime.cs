using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Globalization;

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
            return _value.ToString("O", CultureInfo.InvariantCulture);
        }

        public override void Deserialize(string value)
        {
            DateTime.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _value);
            MarkAsDirty();
        }

        public override void FromBytes(byte[] data)
        {
            long binary = EndianBitConverter.Big.ToInt64(data, 0);
            _value = DateTime.FromBinary(binary);
            MarkAsDirty();
        }

        public override byte[] ToBytes()
        {
            var data = new byte[sizeof(long)];
            EndianBitConverter.Big.CopyBytes(_value.ToBinary(), data, 0);
            return data;
        }

        public override MstJson ToJson()
        {
            return MstJson.Create(_value);
        }

        public override void FromJson(MstJson json)
        {
            _value = json.GetDateTimeValue();
            MarkAsDirty();
        }

        public override void FromJson(string json)
        {
            FromJson(MstJson.Create(json));
        }
    }
}
