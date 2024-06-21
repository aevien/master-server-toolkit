using MasterServerToolkit.Json;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents basic functionality of observable property
    /// </summary>
    public interface IObservableProperty<T> : IObservableProperty
    {
        /// <summary>
        /// Gets current property value
        /// </summary>
        T Value { get; set; }
    }

    public interface IObservableProperty
    {
        /// <summary>
        /// Property key
        /// </summary>
        ushort Key { get; }

        /// <summary>
        /// Invoked, when value gets dirty
        /// </summary>
        event ObservablePropertyDelegate OnDirtyEvent;

        /// <summary>
        /// Inform that property is changed
        /// </summary>
        void MarkAsDirty();

        /// <summary>
        /// Should serialize the whole value to bytes
        /// </summary>
        byte[] ToBytes();

        /// <summary>
        /// Should deserialize value from bytes. 
        /// </summary>
        /// <param name="data"></param>
        void FromBytes(byte[] data);

        /// <summary>
        /// Should serialize a value to string
        /// </summary>
        string Serialize();

        /// <summary>
        /// Should deserialize a value from string
        /// </summary>
        void Deserialize(string value);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        MstJson ToJson();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="json"></param>
        void FromJson(MstJson json);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="json"></param>
        void FromJson(string json);

        /// <summary>
        /// Retrieves updates that happened from the last time
        /// this method was called. If no updates happened - returns null;
        /// </summary>
        byte[] GetUpdates();

        /// <summary>
        /// Updates value according to given data
        /// </summary>
        /// <param name="data"></param>
        void ApplyUpdates(byte[] data);

        /// <summary>
        /// Clears information about accumulated updates.
        /// This is called after property changes are broadcasted to listeners
        /// </summary>
        void ClearUpdates();

        /// <summary>
        /// Cast this property to property of given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        TCast As<TCast>() where TCast : class, IObservableProperty;
    }
}