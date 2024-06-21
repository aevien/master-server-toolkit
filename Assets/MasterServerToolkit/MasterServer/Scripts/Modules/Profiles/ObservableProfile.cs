using MasterServerToolkit.Extensions;
using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Concurrent;
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
        public delegate void ProfilePropertyUpdateDelegate(ushort propertyCode, IObservableProperty property);

        /// <summary>
        /// Check if object is disposed
        /// </summary>
        protected bool isDisposed = false;

        /// <summary>
        /// Properties that are changed and waiting for to be sent
        /// </summary>
        private readonly ConcurrentDictionary<ushort, IObservableProperty> propertiesToBeSent = new ConcurrentDictionary<ushort, IObservableProperty>();

        /// <summary>
        /// Profile properties list
        /// </summary>
        public ConcurrentDictionary<ushort, IObservableProperty> Properties { get; private set; } = new ConcurrentDictionary<ushort, IObservableProperty>();

        /// <summary>
        /// Check if profile has changed properties
        /// </summary>
        public bool HasDirtyProperties { get { return propertiesToBeSent.Count > 0; } }

        /// <summary>
        /// The number of properties the profile has
        /// </summary>
        public int Count { get { return Properties.Count; } }

        /// <summary>
        /// Invoked, when one of the values changes
        /// </summary>
        public event ProfilePropertyUpdateDelegate OnPropertyUpdatedEvent;

        public ObservableProfile() { }

        /// <summary>
        /// Returns an observable value of given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get<T>(ushort key) where T : class, IObservableProperty
        {
            if (!Properties.ContainsKey(key))
            {
                Logs.Error($"Observable property with key [{Extensions.StringExtensions.FromHash(key)}] does not exist");
                return null;
            }

            return Properties[key].As<T>();
        }

        /// <summary>
        /// Returns an observable value of given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get<T>(string key) where T : class, IObservableProperty
        {
            return Get<T>(key.ToUint16Hash());
        }

        /// <summary>
        /// Returns an observable value
        /// </summary>
        public IObservableProperty Get(ushort key)
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
        public bool TryGet<T>(ushort key, out T result) where T : class, IObservableProperty
        {
            bool getResult = Properties.TryGetValue(key, out IObservableProperty val);
            result = val as T;
            return getResult;
        }

        /// <summary>
        /// Tries get propfile property
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TryGet<T>(string key, out T result) where T : class, IObservableProperty
        {
            return TryGet(key.ToUint16Hash(), out result);
        }

        /// <summary>
        /// Adds property to current profile
        /// </summary>
        /// <param name="property"></param>
        public void Add(IObservableProperty property)
        {
            if (Properties.ContainsKey(property.Key)) return;

            //Logs.Debug($"{GetType().Name} adds property with key {property.Key}".ToGreen());

            if (Properties.TryAdd(property.Key, property))
            {
                property.OnDirtyEvent += OnDirtyPropertyEventHandler;
            }
            else
            {
                Logs.Error($"Could not add property {property.Key} to profile");
            }
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
                    writer.Write(Count);

                    foreach (var value in Properties)
                    {
                        //Logs.Debug($"{GetType().Name} to bytes property with key {value.Key}".ToGreen());

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
                        var key = reader.ReadUInt16();
                        var length = reader.ReadInt32();
                        var valueData = reader.ReadBytes(length);

                        if (Properties.ContainsKey(key))
                        {
                            //Logs.Debug($"{GetType().Name} from bytes property with key {Extensions.StringExtensions.FromHash(key)}".ToGreen());
                            Properties[key].FromBytes(valueData);
                        }
                    }
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

            foreach (var property in propertiesToBeSent.Values)
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
            foreach (var property in propertiesToBeSent.Values)
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

            var dataRead = new Dictionary<ushort, byte[]>(count);

            // Read data first, because, in case of an exception
            // we want the pointer of reader to be at the right place 
            // (at the end of current updates)
            for (var i = 0; i < count; i++)
            {
                // Read key
                var key = reader.ReadUInt16();

                // Read length
                var dataLength = reader.ReadInt32();

                // Read update data
                dataRead[key] = reader.ReadBytes(dataLength);
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
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToJson().ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public MstJson ToJson()
        {
            var json = MstJson.EmptyObject;

            foreach (var property in Properties.Values)
            {
                json.AddField(Extensions.StringExtensions.FromHash(property.Key), property.ToJson());
            }

            return json;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="json"></param>
        public void FromJson(string json)
        {
            FromJson(new MstJson(json));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="json"></param>
        public void FromJson(MstJson json)
        {
            foreach (var property in Properties.Values)
            {
                string key = Extensions.StringExtensions.FromHash(property.Key);

                if (json.HasField(key))
                {
                    var value = json[key];

                    if (!value.IsNull)
                    {
                        property.FromJson(value);
                    }
                }
            }
        }

        /// <summary>
        /// Dispose object
        /// </summary>
        public void Dispose()
        {
            propertiesToBeSent.Clear();
            Properties.Clear();
            OnPropertyUpdatedEvent = null;

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
            if (!propertiesToBeSent.ContainsKey(property.Key))
                propertiesToBeSent.TryAdd(property.Key, property);

            //Logs.Info($"<color=#FF0000>{GetType().Name} OnDirtyPropertyEventHandler for {property.Key}</color>");
            OnPropertyUpdatedEvent?.Invoke(property.Key, property);
        }

        /// <summary>
        /// Dispose object safty
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing) { }
    }
}