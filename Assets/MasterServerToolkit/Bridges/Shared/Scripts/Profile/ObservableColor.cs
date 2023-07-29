using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class ObservableColor : ObservableBase<Color>
    {
        public ObservableColor(ushort key) : base(key)
        {
            _value = Color.white;
        }

        public ObservableColor(ushort key, Color color) : base(key, color) { }

        /// <summary>
        /// Just sets current value
        /// </summary>
        /// <param name="value"></param>
        public void Set(Color value)
        {
            if (!_value.Equals(value))
            {
                _value = value;
                MarkDirty();
            }
        }

        public override void ApplyUpdates(byte[] data)
        {
            FromBytes(data);
        }

        public override void ClearUpdates() { }

        public override void Deserialize(string value)
        {
            value = value.StartsWith("#") ? value : $"#{value}";

            if (!ColorUtility.TryParseHtmlString(value, out _value))
                _value = Color.white;
        }

        public override void FromBytes(byte[] data)
        {
            Deserialize(Encoding.UTF8.GetString(data));
            MarkDirty();
        }

        public override byte[] GetUpdates()
        {
            return ToBytes();
        }

        public override string Serialize()
        {
            string value = ColorUtility.ToHtmlStringRGBA(_value);
            return !value.StartsWith("#") ? $"#{value}" : value;
        }

        public override byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes(Serialize());
        }

        public override MstJson ToJson()
        {
            return _value.ToJson();
        }

        public override void FromJson(MstJson json)
        {
            _value = json.ToColor();
        }

        public override void FromJson(string json)
        {
            FromJson(new MstJson(json));
        }
    }
}
