using System;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Base observable value class, which should help out with some things
    /// </summary>
    public abstract class ObservableBase<T> : IObservableProperty<T>, IEquatable<IObservableProperty<T>>
    {
        protected T _value;

        protected ObservableBase(short key)
        {
            Key = key;
        }

        public short Key { get; private set; }

        public event Action<IObservableProperty> OnDirtyEvent;

        public override string ToString()
        {
            return _value.ToString();
        }

        public T GetValue()
        {
            return _value;
        }

        public virtual void MarkDirty()
        {
            OnDirtyEvent?.Invoke(this);
        }

        public abstract byte[] ToBytes();

        public abstract void FromBytes(byte[] data);

        public abstract string Serialize();

        public abstract void Deserialize(string value);

        public abstract byte[] GetUpdates();

        public abstract void ApplyUpdates(byte[] data);

        public abstract void ClearUpdates();

        public TCast CastTo<TCast>() where TCast : class, IObservableProperty
        {
            return this as TCast;
        }

        public bool Equals(IObservableProperty<T> other)
        {
            return Key == other.Key;
        }
    }
}