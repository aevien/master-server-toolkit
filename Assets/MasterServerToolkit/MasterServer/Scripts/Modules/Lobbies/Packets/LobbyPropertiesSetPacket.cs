using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class LobbyPropertiesSetPacket : SerializablePacket
    {
        public int LobbyId { get; set; }
        public MstProperties Properties { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(LobbyId);
            writer.Write(Properties.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            LobbyId = reader.ReadInt32();
            Properties = new MstProperties(reader.ReadDictionary());
        }
    }
}