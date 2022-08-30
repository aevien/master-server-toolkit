using System;

namespace MasterServerToolkit.MasterServer
{
    public class EventMessage : EventArgs
    {
        private object _data;

        public EventMessage() : this(new object()) { }

        public EventMessage(object data)
        {
            _data = data;
        }

        public bool HasData()
        {
            return _data != null;
        }

        public void SetData(object data)
        {
            _data = data;
        }

        public T As<T>()
        {
            return (T)_data;
        }

        public float AsFloat()
        {
            return As<float>();
        }

        public int AsInt()
        {
            return As<int>();
        }

        public string AsString()
        {
            return _data.ToString();
        }

        public bool AsBool()
        {
            return As<bool>();
        }
    }
}
