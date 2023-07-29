using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.IO;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableBaseList<T> : ObservableBase<List<T>>
    {
        public delegate void ObservableBaseListSetEventDelegate(T oldItem, T item);
        public delegate void ObservableBaseListAddEventDelegate(T newItem);
        public delegate void ObservableBaseListInsertEventDelegate(int index, T insertedItem);
        public delegate void ObservableBaseListRemoveEventDelegate(T removedItem);

        private const byte _setOperation = 0;
        private const byte _removeOperation = 1;
        private const byte _insertOperation = 2;

        private Queue<ListUpdateEntry> _updates;
        public int Count => _value.Count;

        public event ObservableBaseListSetEventDelegate OnSetEvent;
        public event ObservableBaseListAddEventDelegate OnAddEvent;
        public event ObservableBaseListInsertEventDelegate OnInsertEvent;
        public event ObservableBaseListRemoveEventDelegate OnRemoveEvent;

        protected ObservableBaseList(ushort key) : this(key, null) { }

        protected ObservableBaseList(ushort key, List<T> defaultValues) : base(key)
        {
            _updates = new Queue<ListUpdateEntry>();
            _value = defaultValues ?? new List<T>();
        }

        /// <summary>
        /// Gets/Sets a value of given type at the specified <paramref name="index"/>
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public T this[int index]
        {
            get
            {
                return _value[index];
            }
            set
            {
                if (value == null)
                {
                    RemoveAt(index);
                    return;
                }

                _value[index] = value;

                _updates.Enqueue(new ListUpdateEntry()
                {
                    index = index,
                    operation = _setOperation,
                    value = value
                });

                MarkDirty();
            }
        }

        /// <summary>
        /// Adds new value of given type to list
        /// </summary>
        /// <param name="value"></param>
        public void Add(T value)
        {
            _value.Add(value);

            _updates.Enqueue(new ListUpdateEntry()
            {
                index = _value.Count - 1,
                operation = _setOperation,
                value = value
            });

            MarkDirty();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        public void AddRange(IEnumerable<T> collection)
        {
            int startIndex = _value.Count > 0 ? _value.Count - 1 : 0;

            _value.AddRange(collection);

            for (int i = startIndex; i < _value.Count; i++)
            {
                T item = _value[i];
                _updates.Enqueue(new ListUpdateEntry()
                {
                    index = i,
                    operation = _setOperation,
                    value = item
                });
            }

            MarkDirty();
        }

        /// <summary>
        /// Removes a value from the list at the specified <paramref name="index"/>
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            _value.RemoveAt(index);

            _updates.Enqueue(new ListUpdateEntry()
            {
                index = index,
                operation = _removeOperation,
            });

            MarkDirty();
        }

        /// <summary>
        /// Tries to remove given value from list
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Remove(T value)
        {
            int index = IndexOf(value);

            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int IndexOf(T value)
        {
            return _value.IndexOf(value);
        }

        /// <summary>
        /// Inserts new item at the specified <paramref name="index"/>
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, T item)
        {
            if (item == null) return;

            _value.Insert(index, item);

            _updates.Enqueue(new ListUpdateEntry()
            {
                index = index,
                operation = _insertOperation,
            });

            MarkDirty();
        }

        /// <summary>
        /// Check if list contains item
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(T item)
        {
            return _value.Contains(item);
        }

        /// <summary>
        /// Write valu to binary data
        /// </summary>
        /// <param name="value"></param>
        /// <param name="writer"></param>
        protected abstract void WriteValue(T value, EndianBinaryWriter writer);

        /// <summary>
        /// Reads value from binary data
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        protected abstract T ReadValue(EndianBinaryReader reader);

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
                        WriteValue(item, writer);
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
                        var value = ReadValue(reader);

                        if (i < _value.Count)
                        {
                            _value[i] = value;
                        }
                        else
                        {
                            _value.Add(value);
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
                        WriteIndex(update.index, writer);

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
                        var index = ReadIndex(reader);

                        if (operation == _removeOperation)
                        {
                            T item = this[index];
                            RemoveAt(index);
                            OnRemoveEvent?.Invoke(item);
                            continue;
                        }

                        var value = ReadValue(reader);

                        if (operation == _insertOperation)
                        {
                            Insert(index, value);
                            OnInsertEvent?.Invoke(index, value);
                            continue;
                        }

                        if (index < _value.Count)
                        {
                            T old = this[index];
                            this[index] = value;
                            OnSetEvent?.Invoke(old, value);
                        }
                        else
                        {
                            Add(value);
                            OnAddEvent?.Invoke(value);
                        }
                    }
                }
            }
        }

        protected void WriteIndex(int index, EndianBinaryWriter writer)
        {
            writer.Write(index);
        }

        protected int ReadIndex(EndianBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        public override void ClearUpdates()
        {
            _updates.Clear();
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var v in _value)
            {
                yield return v;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _value.Count; i++)
            {
                _updates.Enqueue(new ListUpdateEntry()
                {
                    index = i,
                    operation = _removeOperation,
                });
            }

            _value.Clear();

            MarkDirty();
        }

        private struct ListUpdateEntry
        {
            public byte operation;
            public int index;
            public T value;
        }
    }
}