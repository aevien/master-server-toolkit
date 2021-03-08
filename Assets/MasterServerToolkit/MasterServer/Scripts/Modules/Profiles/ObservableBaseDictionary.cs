using MasterServerToolkit.Networking;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableBaseDictionary<TKey, TValue> : ObservableBase<Dictionary<TKey, TValue>>
    {
        private const int _setOperation = 0;
        private const int _removeOperation = 1;
        private Queue<UpdateEntry> _updates;

        protected ObservableBaseDictionary(short key) : this(key, null) { }

        protected ObservableBaseDictionary(short key, Dictionary<TKey, TValue> defaultValues) : base(key)
        {
            _updates = new Queue<UpdateEntry>();
            _value = defaultValues == null ? new Dictionary<TKey, TValue>() :  defaultValues.ToDictionary(k => k.Key, k => k.Value);
        }

        public override string ToString()
        {
            return Serialize();
        }

        /// <summary>
        /// Returns an immutable list of values
        /// </summary>
        public IEnumerable<TValue> Values
        {
            get { return _value.Values; }
        }

        /// <summary>
        /// Returns an immutable list of key-value pairs
        /// </summary>
        public IEnumerable<KeyValuePair<TKey, TValue>> Pairs
        {
            get { return _value.ToList(); }
        }

        /// <summary>
        /// Sets or add a given value by key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetValue(TKey key, TValue value)
        {
            if (value == null)
            {
                Remove(key);
                return;
            }

            _value[key] = value;

            MarkDirty();

            _updates.Enqueue(new UpdateEntry()
            {
                key = key,
                operation = _setOperation,
                value = value
            });
        }

        /// <summary>
        /// Removes value by given key
        /// </summary>
        /// <param name="key"></param>
        public void Remove(TKey key)
        {
            _value.Remove(key);

            MarkDirty();

            _updates.Enqueue(new UpdateEntry()
            {
                key = key,
                operation = _removeOperation,
            });
        }

        /// <summary>
        /// Gets value by given key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TValue GetValue(TKey key)
        {
            return _value[key];
        }

        /// <summary>
        /// Tries to get value by given key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TryGetValue(TKey key, out TValue result)
        {
            bool getResult = _value.TryGetValue(key, out TValue val);
            result = val;
            return getResult;
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
                            _value.Remove(key);
                            continue;
                        }

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
            }

            MarkDirty();
        }

        public override void ClearUpdates()
        {
            _updates.Clear();
        }

        private struct UpdateEntry
        {
            public byte operation;
            public TKey key;
            public TValue value;
        }
    }
}