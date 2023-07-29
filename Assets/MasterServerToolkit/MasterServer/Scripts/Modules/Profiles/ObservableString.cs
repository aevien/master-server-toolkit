using MasterServerToolkit.Json;
using System.Text;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Observable property of type string
    /// </summary>
    public class ObservableString : ObservableBase<string>
    {
        public ObservableString(ushort key, string defaultVal = "") : base(key)
        {
            _value = defaultVal;
        }

        public override string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    MarkDirty();
                }
            }
        }

        public override byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes(_value);
        }

        public override void FromBytes(byte[] data)
        {
            _value = Encoding.UTF8.GetString(data);
            MarkDirty();
        }

        public override string Serialize()
        {
            return _value;
        }

        public override void Deserialize(string value)
        {
            _value = value;
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
            _value = json.StringValue;
        }

        public override void FromJson(string json)
        {
            FromJson(MstJson.Create(json));
        }
    }
}