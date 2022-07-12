using MasterServerToolkit.Networking;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableBaseDictionary<TKey, TValue> : ObservableBase<Dictionary<TKey, TValue>>
    {
        private const int _setOperation = 0;
        private const int _removeOperation = 1;
        private Queue<DictionaryUpdateEntry> _updates;

        protected ObservableBaseDictionary(ushort key) : this(key, null) { }

        protected ObservableBaseDictionary(ushort key, Dictionary<TKey, TValue> defaultValues) : base(key)
        {
            _updates = new Queue<DictionaryUpdateEntry>();
            _value = defaultValues == null ? new Dictionary<TKey, TValue>() : defaultValues.ToDictionary(k => k.Key, k => k.Value);
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
        public IEnumerable<TKey> Keys => _value.Keys;

        /// <summary>
        /// Returns an immutable list of values
        /// </summary>
        public ICollection<TValue> Values => _value.Values;

        /// <summary>
        /// 
        /// </summary>
        public int Count => _value.Count;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="item"></param>
        public void Add(TKey key, TValue item)
        {
            _value.Add(key, item);

            _updates.Enqueue(new DictionaryUpdateEntry()
            {
                key = key,
                operation = _setOperation,
                value = item
            });

            MarkDirty();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(TKey key)
        {
            if (_value.Remove(key))
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
            _value.TryGetValue(key, out result);
            return result != null;
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
                            _value[key] = value;
                        }
                        else
                        {
                            _value.Add(key, value);
                        }
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

                        if (operation == _removeOperation)
                        {
                            Remove(key);
                            continue;
                        }

                        var value = ReadValue(reader);

                        if (ContainsKey(key))
                        {
                            this[key] = value;
                        }
                        else
                        {
                            Add(key, value);
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