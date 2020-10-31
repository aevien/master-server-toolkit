namespace MasterServerToolkit.MasterServer
{
    public class EventMessage
    {
        private object _data;

        public EventMessage() { }

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
    }
}
