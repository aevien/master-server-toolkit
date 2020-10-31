using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// RegistrationPacket, containing data about which player changed which property
    /// </summary>
    public class LobbyMemberPropChangePacket : SerializablePacket
    {
        public int LobbyId { get; set; }
        public string Username { get; set; }
        public string Property { get; set; }
        public string Value { get; set; }

        public LobbyMemberPropChangePacket()
        {
            Username = string.Empty;
            Property = string.Empty;
            Value = string.Empty;
        }


        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(LobbyId);
            writer.Write(Username);
            writer.Write(Property);
            writer.Write(Value);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            LobbyId = reader.ReadInt32();
            Username = reader.ReadString();
            Property = reader.ReadString();
            Value = reader.ReadString();
        }
    }
}