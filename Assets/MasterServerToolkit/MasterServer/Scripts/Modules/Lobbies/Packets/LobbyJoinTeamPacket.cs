using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class LobbyJoinTeamPacket : SerializablePacket
    {
        public int LobbyId { get; set; }
        public string TeamName { get; set; }

        public LobbyJoinTeamPacket()
        {
            LobbyId = 0;
            TeamName = string.Empty;
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(LobbyId);
            writer.Write(TeamName);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            LobbyId = reader.ReadInt32();
            TeamName = reader.ReadString();
        }
    }
}