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
        private readonly bool useDeltaUpdates;
        private int synchronizedValue;

        public ObservableInt(ushort key, int defaultValue = 0, bool useDeltaUpdates = false) : base(key)
        {
            _value = defaultValue;
            synchronizedValue = defaultValue;
            this.useDeltaUpdates = useDeltaUpdates;
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
            long result = (long)_value + value;

            if (result >= int.MinValue && result <= max)
            {
                _value = (int)result;
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
            long result = (long)_value - value;

            if (result >= min && result <= int.MaxValue)
            {
                _value = (int)result;
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
            synchronizedValue = _value;
            MarkAsDirty();
        }

        public override string Serialize()
        {
            return ToJson().ToString();
        }

        public override void Deserialize(string value)
        {
            FromJson(value);
        }

        public override byte[] GetUpdates()
        {
            if (useDeltaUpdates)
            {
                var data = new byte[8];
                EndianBitConverter.Big.CopyBytes((long)_value - synchronizedValue, data, 0);
                return data;
            }

            return ToBytes();
        }

        public override void ApplyUpdates(byte[] data)
        {
            if (useDeltaUpdates)
            {
                long result = _value + EndianBitConverter.Big.ToInt64(data, 0);

                if (result < int.MinValue || result > int.MaxValue)
                    throw new OverflowException("Observable integer delta exceeds Int32 limits");

                _value = (int)result;
                MarkAsDirty();
                return;
            }

            FromBytes(data);
        }

        public override void ClearUpdates()
        {
            if (useDeltaUpdates)
                synchronizedValue = _value;
        }

        public override MstJson ToJson()
        {
            return MstJson.Create(_value);
        }

        public override void FromJson(MstJson json)
        {
            _value = json.IntValue;
            synchronizedValue = _value;
        }

        public override void FromJson(string json)
        {
            FromJson(MstJson.Create(Convert.ToInt32(json)));
        }
    }
}
