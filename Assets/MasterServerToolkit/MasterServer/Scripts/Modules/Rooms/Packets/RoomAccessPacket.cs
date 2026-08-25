using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class RoomAccessPacket : SerializablePacket
    {
        public int Id { get; set; }
        public string Ip { get; set; }
        public ushort Port { get; set; }
        public ushort MaxPlayers { get; set; }
        public string Token { get; set; }
        public string SceneName { get; set; } = string.Empty;
        public MstProperties ExtraParameters { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Token);
            writer.Write(Ip);
            writer.Write(Port);
            writer.Write(MaxPlayers);
            writer.Write(Id);
            writer.Write(SceneName);
            writer.Write(ExtraParameters.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Token = reader.ReadString();
            Ip = reader.ReadString();
            Port = reader.ReadUInt16();
            MaxPlayers = reader.ReadUInt16();
            Id = reader.ReadInt32();
            SceneName = reader.ReadString();
            ExtraParameters = new MstProperties(reader.ReadDictionary());
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("Id", Id);
            options.Add("Ip", Ip);
            options.Add("Port", Port);
            options.Add("MaxPlayers", MaxPlayers);
            options.Add("Token", Token);
            options.Add("SceneName", SceneName);
            options.Append(ExtraParameters);

            return $"[Room Access Info: {options.ToReadableString()}]";
        }
    }
}