using MasterServerToolkit.Logging;
using System;

namespace MasterServerToolkit.MasterServer
{
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
        public event Action<IObservableProperty> OnDirtyEvent;

        /// <summary>
        /// Converts property value to readable string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Serialize();
        }

        public T Value()
        {
            return _value;
        }

        public virtual void MarkDirty()
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

        public abstract byte[] ToBytes();

        public abstract void FromBytes(byte[] data);

        public abstract string Serialize();

        public abstract void Deserialize(string value);

        public abstract byte[] GetUpdates();

        public abstract void ApplyUpdates(byte[] data);

        public abstract void ClearUpdates();
    }
}