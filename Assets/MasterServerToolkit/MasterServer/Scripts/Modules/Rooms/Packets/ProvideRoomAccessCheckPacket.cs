using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ProvideRoomAccessCheckPacket : SerializablePacket
    {
        public int PeerId { get; set; }
        public int RoomId { get; set; }
        public string Username { get; set; } = string.Empty;
        public MstProperties CustomOptions { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(PeerId);
            writer.Write(RoomId);
            writer.Write(Username);
            writer.Write(CustomOptions.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            PeerId = reader.ReadInt32();
            RoomId = reader.ReadInt32();
            Username = reader.ReadString();
            CustomOptions = new MstProperties(reader.ReadDictionary());
        }
    }
}