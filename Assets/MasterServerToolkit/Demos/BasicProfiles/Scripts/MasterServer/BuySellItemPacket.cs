using MasterServerToolkit.Networking;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class BuySellItemPacket : SerializablePacket
    {
        public string Id { get; set; }
        public int Price { get; set; }
        public string Currency { get; set; }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Price = reader.ReadInt32();
            Currency = reader.ReadString();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Price);
            writer.Write(Currency);
        }
    }
}
