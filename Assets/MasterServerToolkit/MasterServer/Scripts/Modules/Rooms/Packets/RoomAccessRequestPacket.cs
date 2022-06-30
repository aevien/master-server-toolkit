using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class RoomAccessRequestPacket : SerializablePacket
    {
        public int RoomId { get; set; }
        public string Password { get; set; } = string.Empty;
        public MstProperties CustomOptions { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(RoomId);
            writer.Write(Password);
            writer.Write(CustomOptions.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            RoomId = reader.ReadInt32();
            Password = reader.ReadString();
            CustomOptions = new MstProperties(reader.ReadDictionary());
        }
    }
}