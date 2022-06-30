using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class LobbyTeamData : SerializablePacket
    {
        public string Name { get; set; } = string.Empty;
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public MstProperties Properties { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(MinPlayers);
            writer.Write(MaxPlayers);
            writer.WriteDictionary(Properties.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Name = reader.ReadString();
            MinPlayers = reader.ReadInt32();
            MaxPlayers = reader.ReadInt32();
            Properties = new MstProperties(reader.ReadDictionary());
        }
    }
}