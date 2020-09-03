using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Observable integer
    /// </summary>
    public class ObservableFloat : ObservableBase<float>
    {
        public ObservableFloat(short key, float defaultValue = 0) : base(key)
        {
            _value = defaultValue;
        }

        public void Add(float val)
        {
            _value += val;
            MarkDirty();
        }

        public void Set(float val)
        {
            if(_value != val)
            {
                _value = val;
                MarkDirty();
            }
        }

        public bool TryTake(float amount)
        {
            if (_value >= amount)
            {
                Add(-amount);
                return true;
            }
            return false;
        }

        public override byte[] ToBytes()
        {
            var data = new byte[4];
            EndianBitConverter.Big.CopyBytes(_value, data, 0);
            return data;
        }

        public override void FromBytes(byte[] data)
        {
            _value = EndianBitConverter.Big.ToSingle(data, 0);
            MarkDirty();
        }

        public override string Serialize()
        {
            return _value.ToString();
        }

        public override void Deserialize(string value)
        {
            _value = float.Parse(value);
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