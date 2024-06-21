using MasterServerToolkit.Networking;
using System.Collections.Generic;
using System.IO;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableBaseList<T> : ObservableBase<List<T>>
    {
        public delegate void ObservableListSetEventDelegate(T oldItem, T item);
        public delegate void ObservableListAddEventDelegate(T newItem);
        public delegate void ObservableListInsertEventDelegate(int index, T insertedItem);
        public delegate void ObservableListRemoveEventDelegate(T removedItem);

        private readonly Queue<ListUpdateEntry> _updates = new Queue<ListUpdateEntry>();
        public int Count => _value.Count;

        public event ObservableListSetEventDelegate OnSetEvent;
        public event ObservableListAddEventDelegate OnAddEvent;
        public event ObservableListInsertEventDelegate OnInsertEvent;
        public event ObservableListRemoveEventDelegate OnRemoveEvent;

        protected ObservableBaseList(ushort key) : this(key, null) { }

        protected ObservableBaseList(ushort key, List<T> defaultValues) : base(key)
        {
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

                var old = _value[index];
                _value[index] = value;

                _updates.Enqueue(new ListUpdateEntry()
                {
                    index = index,
                    operation = ObservableListOperation.Set,
                    value = value
                });

                OnSetEvent?.Invoke(old, value);
                MarkAsDirty();
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
                operation = ObservableListOperation.Set,
                value = value
            });

            OnAddEvent?.Invoke(value);
            MarkAsDirty();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        public void AddRange(IEnumerable<T> collection)
        {
            foreach (var item in collection)
            {
                Add(item);
            }
        }

        /// <summary>
        /// Removes a value from the list at the specified <paramref name="index"/>
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            if (_value.Count > index)
            {
                var removedItem = _value[index];
                _value.RemoveAt(index);

                _updates.Enqueue(new ListUpdateEntry()
                {
                    index = index,
                    operation = ObservableListOperation.Remove,
                });

                OnRemoveEvent?.Invoke(removedItem);
                MarkAsDirty();
            }
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
                operation = ObservableListOperation.Insert,
            });

            OnInsertEvent?.Invoke(index, item);
            MarkAsDirty();
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
                            var oldValue = _value[i];
                            _value[i] = value;
                            OnSetEvent?.Invoke(oldValue, value);
                        }
                        else
                        {
                            _value.Add(value);
                            OnAddEvent?.Invoke(value);
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
                        WriteIndex(update.index, writer);

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
                        var index = ReadIndex(reader);

                        if (operation == ObservableListOperation.Remove)
                        {
                            T item = this[index];
                            _value.RemoveAt(index);
                            OnRemoveEvent?.Invoke(item);
                            continue;
                        }

                        var value = ReadValue(reader);

                        if (operation == ObservableListOperation.Insert)
                        {
                            _value.Insert(index, value);
                            OnInsertEvent?.Invoke(index, value);
                            continue;
                        }

                        if (index < _value.Count)
                        {
                            T old = _value[index];
                            _value[index] = value;
                            OnSetEvent?.Invoke(old, value);
                        }
                        else
                        {
                            _value.Add(value);
                            OnAddEvent?.Invoke(value);
                        }
                    }

                    if (count > 0)
                    {
                        MarkAsDirty();
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
                    operation = ObservableListOperation.Remove,
                });
            }

            _value.Clear();

            MarkAsDirty();
        }

        private struct ListUpdateEntry
        {
            public ObservableListOperation operation;
            public int index;
            public T value;
        }
    }
}