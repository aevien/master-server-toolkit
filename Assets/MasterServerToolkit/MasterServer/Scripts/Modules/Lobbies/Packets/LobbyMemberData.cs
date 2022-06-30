using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Information about a member of the lobby
    /// </summary>
    public class LobbyMemberData : SerializablePacket
    {
        public string Username { get; set; }
        public MstProperties Properties { get; set; }
        public bool IsReady { get; set; }
        public string Team { get; set; }

        public LobbyMemberData()
        {
            Username = string.Empty;
            Properties = new MstProperties();
            Team = string.Empty;
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.WriteDictionary(Properties.ToDictionary());
            writer.Write(IsReady);
            writer.Write(Username);
            writer.Write(Team);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Properties = new MstProperties(reader.ReadDictionary());
            IsReady = reader.ReadBoolean();
            Username = reader.ReadString();
            Team = reader.ReadString();
        }
    }
}