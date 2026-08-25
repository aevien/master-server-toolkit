using MasterServerToolkit.Logging;

namespace MasterServerToolkit.MasterServer
{
    public struct EventPayload
    {
        private object _data;

        public EventPayload(object data)
        {
            _data = data;
        }

        public readonly bool HasData()
        {
            return _data != null;
        }

        public void SetData(object data)
        {
            _data = data;
        }

        public readonly T As<T>()
        {
            return (T)_data;
        }

        public readonly float AsFloat()
        {
            if (HasData())
            {
                return As<float>();
            }
            else
            {
                Logs.Error("No float data found in payload");
                return 0;
            }
        }

        public readonly int AsInt()
        {
            if (HasData())
            {
                return As<int>();
            }
            else
            {
                Logs.Error("No int data found in payload");
                return 0;
            }
        }

        public readonly string AsString()
        {
            if (HasData())
            {
                return _data.ToString();
            }
            else
            {
                Logs.Error("No string data found in payload");
                return string.Empty;
            }
        }

        public readonly bool AsBool()
        {
            if (HasData())
            {
                return As<bool>();
            }
            else
            {
                Logs.Error("No boolean data found in payload");
                return false;
            }
        }
    }
}
