using MasterServerToolkit.Json;
using System;

namespace MasterServerToolkit.MasterServer
{
    public delegate void ObservablePropertyDelegate(IObservableProperty property);

    /// <summary>
    /// Base observable value class, which should help out with some things
    /// </summary>
    public abstract class ObservableBase<T> : IObservableProperty<T>, IEquatable<IObservableProperty<T>>
    {
        protected T _value;

        protected ObservableBase(ushort key)
        {
            Key = key;
        }

        protected ObservableBase(ushort key, T value)
        {
            Key = key;
            _value = value;
        }

        public ushort Key { get; private set; }
        public event ObservablePropertyDelegate OnDirtyEvent;

        /// <summary>
        /// Converts property value to readable string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Serialize();
        }

        /// <summary>
        /// Sets a value of this property
        /// </summary>
        public virtual T Value
        {
            get { return _value; }
            set
            {
                _value = value;
                MarkAsDirty();
            }
        }

        public virtual void MarkAsDirty()
        {
            OnDirtyEvent?.Invoke(this);
        }

        public TCast As<TCast>() where TCast : class, IObservableProperty
        {
            return this as TCast;
        }

        public bool Equals(IObservableProperty<T> other)
        {
            return Key == other.Key;
        }

        public string ToBase64String()
        {
            return Convert.ToBase64String(ToBytes());
        }

        public void FromBase64String(string data)
        {
            FromBytes(Convert.FromBase64String(data));
        }

        public abstract byte[] ToBytes();

        public abstract void FromBytes(byte[] data);

        public abstract string Serialize();

        public abstract void Deserialize(string value);

        public abstract MstJson ToJson();

        public abstract void FromJson(MstJson json);

        public abstract void FromJson(string json);

        public abstract byte[] GetUpdates();

        public abstract void ApplyUpdates(byte[] data);

        public abstract void ClearUpdates();
    }
}