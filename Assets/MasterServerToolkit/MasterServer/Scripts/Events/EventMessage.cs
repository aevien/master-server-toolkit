using System;

namespace MasterServerToolkit.MasterServer
{
    public class EventMessage
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

        public T GetData<T>() where T : class
        {
            return (T)_data;
        }

        public int AsInt()
        {
            try
            {
                return Convert.ToInt32(_data);
            }
            catch
            {
                return -1;
            }
        }

        public string AsString()
        {
            return _data.ToString();
        }
    }
}
