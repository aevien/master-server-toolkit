using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class UsernameAndPeerIdPacket : SerializablePacket
    {
        public string Username { get; set; } = string.Empty;
        public int PeerId { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Username);
            writer.Write(PeerId);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Username = reader.ReadString();
            PeerId = reader.ReadInt32();
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("Username", Username);
            options.Add("PeerId", PeerId);

            return options.ToReadableString();
        }
    }
}