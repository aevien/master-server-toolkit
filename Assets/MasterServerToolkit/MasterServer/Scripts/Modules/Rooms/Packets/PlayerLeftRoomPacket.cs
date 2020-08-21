using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class PlayerLeftRoomPacket : SerializablePacket
    {
        public int PeerId { get; set; }
        public int RoomId { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(PeerId);
            writer.Write(RoomId);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            PeerId = reader.ReadInt32();
            RoomId = reader.ReadInt32();
        }
    }
}