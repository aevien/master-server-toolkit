using MasterServerToolkit.Networking;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableBaseDictionary<TKey, TValue> : ObservableBase<ConcurrentDictionary<TKey, TValue>>
    {
        public delegate void ObservableDictionarySetEventDelegate(TKey key, TValue oldValue, TValue newValue);
        public delegate void ObservableDictionaryAddEventDelegate(TKey newKey, TValue newValue);
        public delegate void ObservableDictionaryRemoveEventDelegate(TKey key, TValue removedValue);

        private readonly Queue<DictionaryUpdateEntry> _updates = new Queue<DictionaryUpdateEntry>();

        public event ObservableDictionarySetEventDelegate OnSetEvent;
        public event ObservableDictionaryAddEventDelegate OnAddEvent;
        public event ObservableDictionaryRemoveEventDelegate OnRemoveEvent;

        protected ObservableBaseDictionary(ushort key) : this(key, null) { }

        protected ObservableBaseDictionary(ushort key, ConcurrentDictionary<TKey, TValue> defaultValues) : base(key)
        {
            _value = defaultValues == null ? new ConcurrentDictionary<TKey, TValue>() : defaultValues;
        }

        public TValue this[TKey key]
        {
            get
            {
                return _value[key];
            }
            set
            {
                if (value == null)
                {
                    Remove(key);
                    return;
                }

                var oldValue = _value[key];
                _value[key] = value;

                _updates.Enqueue(new DictionaryUpdateEntry()
                {
                    key = key,
                    operation = ObservableListOperation.Set,
                    value = value
                });

                OnSetEvent?.Invoke(key, oldValue, value);
                MarkAsDirty();
            }
        }

        /// <summary>
        /// Returns an immutable list of keys
        /// </summary>
        public IEnumerable<TKey> Keys
        {
            get
            {
                return _value.Keys;
            }
        }

        /// <summary>
        /// Returns an immutable list of values
        /// </summary>
        public ICollection<TValue> Values
        {
            get
            {
                return _value.Values;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public int Count
        {
            get
            {
                return _value.Count;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="item"></param>
        public void Add(TKey key, TValue item)
        {
            if (_value.TryAdd(key, item))
            {
                _updates.Enqueue(new DictionaryUpdateEntry()
                {
                    key = key,
                    operation = ObservableListOperation.Set,
                    value = item
                });

                OnAddEvent?.Invoke(key, item);
                MarkAsDirty();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(TKey key)
        {
            if (_value.TryRemove(key, out TValue removedItem))
            {
                _updates.Enqueue(new DictionaryUpdateEntry()
                {
                    key = key,
                    operation = ObservableListOperation.Remove,
                });

                OnRemoveEvent?.Invoke(key, removedItem);
                MarkAsDirty();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to get value by given key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TryGetValue(TKey key, out TValue result)
        {
            return _value.TryGetValue(key, out result);
        }

        /// <summary>
        /// Write valu to binary data
        /// </summary>
        /// <param name="value"></param>
        /// <param name="writer"></param>
        protected abstract void WriteValue(TValue value, EndianBinaryWriter writer);

        /// <summary>
        /// Reads value from binary data
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        protected abstract TValue ReadValue(EndianBinaryReader reader);

        /// <summary>
        /// Writes key to binary data
        /// </summary>
        /// <param name="key"></param>
        /// <param name="writer"></param>
        protected abstract void WriteKey(TKey key, EndianBinaryWriter writer);

        /// <summary>
        /// Reads key from binary data
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        protected abstract TKey ReadKey(EndianBinaryReader reader);

        public override byte[] ToBytes()
        {
            byte[] b;
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.Write(_value.Count);

                    foreach (var item in _value)
                    {
                        WriteKey(item.Key, writer);
                        WriteValue(item.Value, writer);
                    }
                }

                b = ms.ToArray();
            }
            return b;
        }

        public override void FromBytes(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var count = reader.ReadInt32();

                    for (var i = 0; i < count; i++)
                    {
                        var key = ReadKey(reader);
                        var value = ReadValue(reader);

                        if (_value.ContainsKey(key))
                        {
                            var oldValue = _value[key];
                            _value[key] = value;
                            OnSetEvent?.Invoke(key, oldValue, value);
                        }
                        else
                        {
                            _value.TryAdd(key, value);
                            OnAddEvent?.Invoke(key, value);
                        }
                    }
                }

                MarkAsDirty();
            }
        }

        public override byte[] GetUpdates()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.Write(_updates.Count);

                    foreach (var update in _updates)
                    {
                        writer.Write((byte)update.operation);
                        WriteKey(update.key, writer);

                        if (update.operation != ObservableListOperation.Remove)
                        {
                            WriteValue(update.value, writer);
                        }
                    }
                }

                return ms.ToArray();
            }
        }

        public override void ApplyUpdates(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var count = reader.ReadInt32();

                    for (var i = 0; i < count; i++)
                    {
                        var operation = (ObservableListOperation)reader.ReadByte();
                        var key = ReadKey(reader);

                        if (operation == ObservableListOperation.Remove
                            && _value.TryRemove(key, out var valueToBeRemoved))
                        {
                            OnRemoveEvent?.Invoke(key, valueToBeRemoved);
                            continue;
                        }

                        var value = ReadValue(reader);

                        if (ContainsKey(key))
                        {
                            var oldValue = _value[key];
                            _value[key] = value;
                            OnSetEvent?.Invoke(key, oldValue, value);
                        }
                        else
                        {
                            _value.TryAdd(key, value);
                            OnAddEvent?.Invoke(key, value);
                        }
                    }
                }
            }
        }

        public override void ClearUpdates()
        {
            _updates.Clear();
        }

        public bool ContainsKey(TKey key)
        {
            return _value.ContainsKey(key);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (KeyValuePair<TKey, TValue> item in _value)
                yield return item;
        }

        private struct DictionaryUpdateEntry
        {
            public ObservableListOperation operation;
            public TKey key;
            public TValue value;
        }
    }
}