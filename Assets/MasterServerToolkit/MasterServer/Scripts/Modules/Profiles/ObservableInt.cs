using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Observable integer
    /// </summary>
    public class ObservableInt : ObservableBase<int>
    {
        public ObservableInt(ushort key, int defaultValue = 0) : base(key)
        {
            _value = defaultValue;
        }

        /// <summary>
        /// Increments current value by <paramref name="value"/>
        /// </summary>
        /// <param name="value"></param>
        public void Add(int value)
        {
            _value += value;
            MarkDirty();
        }

        /// <summary>
        /// Just sets current value
        /// </summary>
        /// <param name="value"></param>
        public void Set(int value)
        {
            if (_value != value)
            {
                _value = value;
                MarkDirty();
            }
        }

        /// <summary>
        /// Tries to take given <paramref name="amount"/> away from current value
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
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
        }

        public override void ClearUpdates() { }
    }
}