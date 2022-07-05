using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public abstract class BaseListPacket<T> : SerializablePacket
    {
        public List<T> Items { get; set; }

        public BaseListPacket()
        {
            Items = new List<T>();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Items.Count);

            foreach (T item in Items)
                WriteItem(item, writer);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
                Items.Add(ReadItem(reader));
        }

        protected abstract void WriteItem(T item, EndianBinaryWriter writer);
        protected abstract T ReadItem(EndianBinaryReader reader);
    }
}