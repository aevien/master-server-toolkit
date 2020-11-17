using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class PeerAccountInfoPacket : SerializablePacket
    {
        public int PeerId { get; set; }
        public string Username { get; set; }
        public string UserId { get; set; }
        public MstProperties Properties { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(PeerId);
            writer.Write(Username);
            writer.Write(UserId);
            writer.Write(Properties.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            PeerId = reader.ReadInt32();
            Username = reader.ReadString();
            UserId = reader.ReadString();
            Properties = new MstProperties(reader.ReadDictionary());
        }

        public override string ToString()
        {
            return string.Format($"[Peer account info: Peer ID: {PeerId}, UserId: {UserId}, Username: {Username}, Properties: {Properties}]");
        }
    }
}