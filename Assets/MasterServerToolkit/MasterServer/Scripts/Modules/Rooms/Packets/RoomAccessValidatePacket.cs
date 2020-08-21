using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class RoomAccessValidatePacket : SerializablePacket
    {
        public string Token { get; set; }
        public int RoomId { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Token);
            writer.Write(RoomId);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Token = reader.ReadString();
            RoomId = reader.ReadInt32();
        }
    }
}