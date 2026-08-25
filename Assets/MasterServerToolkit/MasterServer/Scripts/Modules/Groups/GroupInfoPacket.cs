using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class GroupInfoPacket : SerializablePacket
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OwnerAccountId { get; set; } = string.Empty;
        public string OwnerUsername { get; set; } = string.Empty;
        public string ChatChannelName { get; set; } = string.Empty;
        public MstGroupJoinPolicy JoinPolicy { get; set; } = MstGroupJoinPolicy.InviteOnly;
        public int MaxMembers { get; set; }
        public MstGroupRole CurrentUserRole { get; set; } = MstGroupRole.Member;

        public bool IsEmpty => string.IsNullOrWhiteSpace(Id);

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Name);
            writer.Write(Tag);
            writer.Write(Description);
            writer.Write(OwnerAccountId);
            writer.Write(OwnerUsername);
            writer.Write(ChatChannelName);
            writer.Write((byte)JoinPolicy);
            writer.Write(MaxMembers);
            writer.Write((byte)CurrentUserRole);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Name = reader.ReadString();
            Tag = reader.ReadString();
            Description = reader.ReadString();
            OwnerAccountId = reader.ReadString();
            OwnerUsername = reader.ReadString();
            ChatChannelName = reader.ReadString();
            JoinPolicy = (MstGroupJoinPolicy)reader.ReadByte();
            MaxMembers = reader.ReadInt32();
            CurrentUserRole = (MstGroupRole)reader.ReadByte();
        }

        public static GroupInfoPacket Empty()
        {
            return new GroupInfoPacket();
        }

        public static GroupInfoPacket FromGroup(MstGroupInfo group, MstGroupMemberInfo currentMember)
        {
            if (group == null)
                return Empty();

            return new GroupInfoPacket
            {
                Id = group.Id,
                Name = group.Name,
                Tag = group.Tag,
                Description = group.Description,
                OwnerAccountId = group.OwnerAccountId,
                OwnerUsername = group.OwnerUsername,
                ChatChannelName = group.ChatChannelName,
                JoinPolicy = (MstGroupJoinPolicy)group.JoinPolicy,
                MaxMembers = group.MaxMembers,
                CurrentUserRole = currentMember == null ? MstGroupRole.Member : (MstGroupRole)currentMember.Role
            };
        }
    }
}
