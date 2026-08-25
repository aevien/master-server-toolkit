using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ChatChannelUserPacket : SerializablePacket
    {
        public string ChannelName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public ChatChannelPermission Permissions { get; set; } = ChatChannelPermission.DefaultMember;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(ChannelName);
            writer.Write(Username);
            writer.Write((int)Permissions);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            ChannelName = reader.ReadString();
            Username = reader.ReadString();
            Permissions = (ChatChannelPermission)reader.ReadInt32();
        }
    }
}
