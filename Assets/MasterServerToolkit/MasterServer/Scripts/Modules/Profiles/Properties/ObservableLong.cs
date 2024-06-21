using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableLong : ObservableBase<long>
    {
        public ObservableLong(ushort key, long defaultValue = 0) : base(key)
        {
            _value = defaultValue;
        }

        public override long Value
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
        public bool Add(long value, long max = long.MaxValue)
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
        public bool Subtract(long value, long min = long.MinValue)
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
            var data = new byte[8];
            EndianBitConverter.Big.CopyBytes(_value, data, 0);
            return data;
        }

        public override void FromBytes(byte[] data)
        {
            _value = EndianBitConverter.Big.ToInt64(data, 0);
            MarkAsDirty();
        }

        public override string Serialize()
        {
            return _value.ToString();
        }

        public override void Deserialize(string value)
        {
            _value = long.Parse(value);
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
            _value = json.LongValue;
        }

        public override void FromJson(string json)
        {
            FromJson(new MstJson(Convert.ToInt64(json)));
        }
    }
}