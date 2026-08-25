using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class GroupInvitePacket : SerializablePacket
    {
        public string InviteId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string GroupTag { get; set; } = string.Empty;
        public string FromUsername { get; set; } = string.Empty;
        public string ToUsername { get; set; } = string.Empty;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(InviteId);
            writer.Write(GroupId);
            writer.Write(GroupName);
            writer.Write(GroupTag);
            writer.Write(FromUsername);
            writer.Write(ToUsername);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            InviteId = reader.ReadString();
            GroupId = reader.ReadString();
            GroupName = reader.ReadString();
            GroupTag = reader.ReadString();
            FromUsername = reader.ReadString();
            ToUsername = reader.ReadString();
        }
    }
}
