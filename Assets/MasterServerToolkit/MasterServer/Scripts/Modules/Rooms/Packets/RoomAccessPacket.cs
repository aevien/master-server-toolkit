using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class RoomAccessPacket : SerializablePacket
    {
        public string RoomIp { get; set; }
        public ushort RoomPort { get; set; }
        public ushort RoomMaxConnections { get; set; }
        public string Token { get; set; }
        public int RoomId { get; set; }
        public string SceneName { get; set; } = string.Empty;
        public MstProperties CustomOptions { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Token);
            writer.Write(RoomIp);
            writer.Write(RoomPort);
            writer.Write(RoomMaxConnections);
            writer.Write(RoomId);
            writer.Write(SceneName);
            writer.Write(CustomOptions.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Token = reader.ReadString();
            RoomIp = reader.ReadString();
            RoomPort = reader.ReadUInt16();
            RoomMaxConnections = reader.ReadUInt16();
            RoomId = reader.ReadInt32();
            SceneName = reader.ReadString();
            CustomOptions = new MstProperties(reader.ReadDictionary());
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("RoomIp", RoomIp);
            options.Add("RoomPort", RoomPort);
            options.Add("RoomMaxConnections", RoomMaxConnections);
            options.Add("RoomId", RoomId);
            options.Add("Token", Token);
            options.Add("SceneName", SceneName);
            options.Append(CustomOptions);

            return $"[Room Access Info: {options.ToReadableString()}]";
        }
    }
}