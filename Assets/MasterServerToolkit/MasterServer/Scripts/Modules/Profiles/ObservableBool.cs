using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableBool : ObservableBase<bool>
    {
        public ObservableBool(ushort key, bool defaultValue = false) : base(key)
        {
            _value = defaultValue;
        }

        public void Set(bool val)
        {
            if (_value != val)
            {
                _value = val;
                MarkDirty();
            }
        }

        public override byte[] ToBytes()
        {
            var data = new byte[4];
            EndianBitConverter.Big.CopyBytes(_value, data, 0);

            return data;
        }

        public override void FromBytes(byte[] data)
        {
            _value = EndianBitConverter.Big.ToBoolean(data, 0);
            MarkDirty();
        }

        public override string Serialize()
        {
            return _value.ToString();
        }

        public override void Deserialize(string value)
        {
            _value = bool.Parse(value);
        }

        public override byte[] GetUpdates()
        {
            return ToBytes();
        }

        public override void ApplyUpdates(byte[] data)
        {
            FromBytes(data);
        }

        public override void ClearUpdates() { }
    }
}