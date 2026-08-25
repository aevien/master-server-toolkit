using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class GroupCreatePacket : SerializablePacket
    {
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MstGroupJoinPolicy JoinPolicy { get; set; } = MstGroupJoinPolicy.InviteOnly;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Tag);
            writer.Write(Description);
            writer.Write((byte)JoinPolicy);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Name = reader.ReadString();
            Tag = reader.ReadString();
            Description = reader.ReadString();
            JoinPolicy = (MstGroupJoinPolicy)reader.ReadByte();
        }
    }
}
