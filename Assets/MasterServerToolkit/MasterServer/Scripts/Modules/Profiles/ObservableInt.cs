using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Observable integer
    /// </summary>
    public class ObservableInt : ObservableBase<int>
    {
        public ObservableInt(short key, int defaultValue = 0) : base(key)
        {
            _value = defaultValue;
        }

        public void Add(int val)
        {
            _value += val;
            MarkDirty();
        }

        public void Set(int val)
        {
            if (_value != val)
            {
                _value = val;
                MarkDirty();
            }
        }

        public bool TryTake(int amount)
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
            _value = EndianBitConverter.Big.ToInt32(data, 0);
            MarkDirty();
        }

        public override string Serialize()
        {
            return _value.ToString();
        }

        public override void Deserialize(string value)
        {
            _value = int.Parse(value);
        }

        public override byte[] GetUpdates()
        {
            return ToBytes();
        }

        public override void ApplyUpdates(byte[] data)
        {
            FromBytes(data);
            MarkDirty();
        }

        public override void ClearUpdates() { }
    }
}