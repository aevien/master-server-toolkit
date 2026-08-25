using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ChatChannelOptionsPacket : SerializablePacket
    {
        public string ChannelName { get; set; } = string.Empty;
        public ChatChannelType Type { get; set; } = ChatChannelType.Public;
        public bool IsClosed { get; set; }
        public bool IsPersistent { get; set; }
        public int MaxHistory { get; set; } = 100;
        public ChatHistoryVisibility HistoryVisibility { get; set; } = ChatHistoryVisibility.MembersOnly;
        public ChatChannelPermission DefaultMemberPermissions { get; set; } = ChatChannelPermission.DefaultMember;
        public ChatChannelPermission InvitedMemberPermissions { get; set; } = ChatChannelPermission.DefaultMember;

        public static ChatChannelOptionsPacket FromOptions(string channelName, ChatChannelOptions options)
        {
            options = options ?? new ChatChannelOptions();

            return new ChatChannelOptionsPacket
            {
                ChannelName = channelName,
                Type = options.Type,
                IsClosed = options.IsClosed,
                IsPersistent = options.IsPersistent,
                MaxHistory = options.MaxHistory,
                HistoryVisibility = options.HistoryVisibility,
                DefaultMemberPermissions = options.DefaultMemberPermissions,
                InvitedMemberPermissions = options.InvitedMemberPermissions
            };
        }

        public ChatChannelOptions ToOptions(string ownerUsername)
        {
            return new ChatChannelOptions
            {
                Type = Type,
                OwnerUsername = ownerUsername,
                IsClosed = IsClosed,
                IsPersistent = IsPersistent,
                MaxHistory = MaxHistory,
                HistoryVisibility = HistoryVisibility,
                DefaultMemberPermissions = DefaultMemberPermissions,
                InvitedMemberPermissions = InvitedMemberPermissions
            };
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(ChannelName);
            writer.Write((int)Type);
            writer.Write(IsClosed);
            writer.Write(IsPersistent);
            writer.Write(MaxHistory);
            writer.Write((int)HistoryVisibility);
            writer.Write((int)DefaultMemberPermissions);
            writer.Write((int)InvitedMemberPermissions);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            ChannelName = reader.ReadString();
            Type = (ChatChannelType)reader.ReadInt32();
            IsClosed = reader.ReadBoolean();
            IsPersistent = reader.ReadBoolean();
            MaxHistory = reader.ReadInt32();
            HistoryVisibility = (ChatHistoryVisibility)reader.ReadInt32();
            DefaultMemberPermissions = (ChatChannelPermission)reader.ReadInt32();
            InvitedMemberPermissions = (ChatChannelPermission)reader.ReadInt32();
        }
    }
}
