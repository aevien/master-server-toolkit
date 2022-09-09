using MasterServerToolkit.Networking;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableBaseDictionary<TKey, TValue> : ObservableBase<ConcurrentDictionary<TKey, TValue>>
    {
        public delegate void ObservableBaseDictionarySetEventDelegate(TKey key, TValue oldValue, TValue newValue);
        public delegate void ObservableBaseDictionaryAddEventDelegate(TKey newKey, TValue newValue);
        public delegate void ObservableBaseDictionaryRemoveEventDelegate(TKey key, TValue removedValue);

        private const int _setOperation = 0;
        private const int _removeOperation = 1;
        private Queue<DictionaryUpdateEntry> _updates;

        public event ObservableBaseDictionarySetEventDelegate OnSetEvent;
        public event ObservableBaseDictionaryAddEventDelegate OnAddEvent;
        public event ObservableBaseDictionaryRemoveEventDelegate OnRemoveEvent;

        protected ObservableBaseDictionary(ushort key) : this(key, null) { }

        protected ObservableBaseDictionary(ushort key, ConcurrentDictionary<TKey, TValue> defaultValues) : base(key)
        {
            _updates = new Queue<DictionaryUpdateEntry>();
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

                _value[key] = value;

                _updates.Enqueue(new DictionaryUpdateEntry()
                {
                    key = key,
                    operation = _setOperation,
                    value = value
                });

                MarkDirty();
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
                    operation = _setOperation,
                    value = item
                });

                MarkDirty();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(TKey key)
        {
            if (_value.TryRemove(key, out _))
            {
                _updates.Enqueue(new DictionaryUpdateEntry()
                {
                    key = key,
                    operation = _removeOperation,
                });

                MarkDirty();
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
                            _value[key] = value;
                        else
                            _value.TryAdd(key, value);
                    }
                }

                MarkDirty();
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
                        writer.Write(update.operation);
                        WriteKey(update.key, writer);

                        if (update.operation != _removeOperation)
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
                        var operation = reader.ReadByte();
                        var key = ReadKey(reader);

                        if (operation == _removeOperation
                            && _value.TryRemove(key, out var valueToBeRemoved))
                        {
                            OnRemoveEvent?.Invoke(key, valueToBeRemoved);
                            continue;
                        }

                        var value = ReadValue(reader);

                        if (ContainsKey(key))
                        {
                            var oldValue = this[key];
                            this[key] = value;
                            OnSetEvent?.Invoke(key, oldValue, value);
                        }
                        else
                        {
                            Add(key, value);
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
            public byte operation;
            public TKey key;
            public TValue value;
        }
    }
}