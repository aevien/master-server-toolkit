using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;

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

        public override int Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    MarkAsDirty();
                }
            }
        }

        /// <summary>
        /// Increments current value by <paramref name="value"/>
        /// </summary>
        /// <param name="value"></param>
        public bool Add(int value, int max = int.MaxValue)
        {
            if (_value + value <= max)
            {
                _value += value;
                MarkAsDirty();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Decrements current value by <paramref name="value"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <returns></returns>
        public bool Subtract(int value, int min = int.MinValue)
        {
            if (_value - value >= min)
            {
                _value -= value;
                MarkAsDirty();
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
            MarkAsDirty();
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

        public override MstJson ToJson()
        {
            return MstJson.Create(_value);
        }

        public override void FromJson(MstJson json)
        {
            _value = json.IntValue;
        }

        public override void FromJson(string json)
        {
            FromJson(new MstJson(Convert.ToInt32(json)));
        }
    }
}