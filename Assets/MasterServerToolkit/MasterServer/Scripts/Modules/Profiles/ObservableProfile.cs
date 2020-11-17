using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents clients profile, which emits events about changes.
    /// Client, game server and master servers will create a similar
    /// object.
    /// </summary>
    public class ObservableProfile : IEnumerable<IObservableProperty>, IDisposable
    {
        public delegate void PropertyUpdateHandler(short propertyCode, IObservableProperty property);

        /// <summary>
        /// Check if object is disposed
        /// </summary>
        protected bool isDisposed = false;

        /// <summary>
        /// Properties that are changed and waiting for to be sent
        /// </summary>
        private HashSet<IObservableProperty> propertiesToBeSent;

        /// <summary>
        /// Profile properties list
        /// </summary>
        public Dictionary<short, IObservableProperty> Properties { get; protected set; }

        /// <summary>
        /// Invoked, when one of the values changes
        /// </summary>
        public event Action<short, IObservableProperty> OnPropertyUpdatedEvent;

        public ObservableProfile()
        {
            Properties = new Dictionary<short, IObservableProperty>();
            propertiesToBeSent = new HashSet<IObservableProperty>();
        }

        /// <summary>
        /// Check if profile has changed properties
        /// </summary>
        public bool HasDirtyProperties { get { return propertiesToBeSent.Count > 0; } }

        /// <summary>
        /// The number of properties the profile has
        /// </summary>
        public int PropertyCount { get { return Properties.Count; } }

        /// <summary>
        /// Returns an observable value of given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T GetProperty<T>(short key) where T : class, IObservableProperty
        {
            if (!Properties.ContainsKey(key))
            {
                Logs.Error($"Observable property with key [{key}] does not exist");
                return null;
            }

            return Properties[key].CastTo<T>();
        }

        /// <summary>
        /// Returns an observable value
        /// </summary>
        public IObservableProperty GetProperty(short key)
        {
            return Properties[key];
        }

        /// <summary>
        /// Tries get propfile property
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TryGetProperty<T>(short key, out T result) where T : class, IObservableProperty
        {
            bool getResult = Properties.TryGetValue(key, out IObservableProperty val);
            result = val as T;
            return getResult;
        }

        /// <summary>
        /// Adds property to current profile
        /// </summary>
        /// <param name="property"></param>
        public void AddProperty(IObservableProperty property)
        {
            if (Properties.ContainsKey(property.Key)) return;

            Properties.Add(property.Key, property);
            property.OnDirtyEvent += OnDirtyPropertyEventHandler;
        }

        /// <summary>
        /// Adds property to current profile
        /// </summary>
        /// <param name="property"></param>
        public void Add(IObservableProperty property)
        {
            AddProperty(property);
        }

        /// <summary>
        /// Writes all data from profile to buffer
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, stream))
                {
                    // Write count
                    writer.Write(Properties.Count);

                    foreach (var value in Properties)
                    {
                        // Write key
                        writer.Write(value.Key);

                        var data = value.Value.ToBytes();

                        // Write data length
                        writer.Write(data.Length);

                        // Write data
                        writer.Write(data);
                    }
                }
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Restores profile from data in the buffer
        /// </summary>
        public void FromBytes(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var count = reader.ReadInt32();

                    for (int i = 0; i < count; i++)
                    {
                        var key = reader.ReadInt16();
                        var length = reader.ReadInt32();
                        var valueData = reader.ReadBytes(length);

                        if (Properties.ContainsKey(key))
                        {
                            Properties[key].FromBytes(valueData);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Restores profile from dictionary of strings
        /// </summary>
        /// <param name="dataData"></param>
        public void FromStrings(Dictionary<short, string> dataData)
        {
            foreach (var pair in dataData)
            {
                Properties.TryGetValue(pair.Key, out IObservableProperty property);

                if (property != null)
                {
                    property.Deserialize(pair.Value);
                }
            }
        }

        /// <summary>
        /// Returns observable properties changes, writen to byte array
        /// </summary>
        /// <returns></returns>
        public byte[] GetUpdates()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    WriteUpdates(writer);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Writes changes into the writer
        /// </summary>
        /// <param name="writer"></param>
        public void WriteUpdates(EndianBinaryWriter writer)
        {
            // Write values count
            writer.Write(propertiesToBeSent.Count);

            foreach (var property in propertiesToBeSent)
            {
                // Write key
                writer.Write(property.Key);

                var updates = property.GetUpdates();

                // Write udpates length
                writer.Write(updates.Length);

                // Write actual updates
                writer.Write(updates);
            }
        }

        /// <summary>
        /// Clears all updates in properties
        /// </summary>
        public void ClearUpdates()
        {
            foreach (var property in propertiesToBeSent)
            {
                property.ClearUpdates();
            }

            propertiesToBeSent.Clear();
        }

        /// <summary>
        /// Uses updates data to update values in the profile
        /// </summary>
        /// <param name="updates"></param>
        public void ApplyUpdates(byte[] updates)
        {
            using (var ms = new MemoryStream(updates))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    ReadUpdates(reader);
                }
            }
        }

        /// <summary>
        /// Use updates data to update values in the profile
        /// </summary>
        /// <param name="updates"></param>
        public void ReadUpdates(EndianBinaryReader reader)
        {
            // Read count
            var count = reader.ReadInt32();

            var dataRead = new Dictionary<short, byte[]>(count);

            // Read data first, because, in case of an exception
            // we want the pointer of reader to be at the right place 
            // (at the end of current updates)
            for (var i = 0; i < count; i++)
            {
                // Read key
                var key = reader.ReadInt16();

                // Read length
                var dataLength = reader.ReadInt32();

                // Read update data
                var data = reader.ReadBytes(dataLength);

                if (!dataRead.ContainsKey(key))
                {
                    //UnityEngine.Debug.LogError($"Property {key} updated");

                    dataRead.Add(key, data);
                }
            }

            // Update observables
            foreach (var updateEntry in dataRead)
            {
                if (Properties.TryGetValue(updateEntry.Key, out IObservableProperty property))
                {
                    property.ApplyUpdates(updateEntry.Value);
                }
            }
        }

        /// <summary>
        /// Serializes all of the properties to dictionary
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> ToStringsDictionary()
        {
            var dict = new Dictionary<string, string>();

            foreach (var pair in Properties)
            {
                dict.Add(pair.Key.ToString(), pair.Value.Serialize());
            }

            return dict;
        }

        /// <summary>
        /// Dispose object
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerator<IObservableProperty> GetEnumerator()
        {
            return Properties.Values.GetEnumerator();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Called, when a value becomes dirty
        /// </summary>
        /// <param name="property"></param>
        protected virtual void OnDirtyPropertyEventHandler(IObservableProperty property)
        {
            if (!propertiesToBeSent.Contains(property))
                propertiesToBeSent.Add(property);

            OnPropertyUpdatedEvent?.Invoke(property.Key, property);
        }

        /// <summary>
        /// Dispose object safty
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing) { }
    }
}
