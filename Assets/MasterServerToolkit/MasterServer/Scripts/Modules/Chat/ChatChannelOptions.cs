using System;

namespace MasterServerToolkit.MasterServer
{
    public enum ChatChannelType
    {
        Public,
        PrivateGroup,
        Direct,
        System
    }

    public enum ChatHistoryVisibility
    {
        None,
        MembersOnly,
        SinceJoined,
        Everyone
    }

    [Flags]
    public enum ChatChannelPermission
    {
        None = 0,
        View = 1,
        Join = 2,
        Send = 4,
        ViewUsers = 8,
        Invite = 16,
        Kick = 32,
        Ban = 64,
        Mute = 128,
        ManageChannel = 256,
        ReadHistory = 512,

        DefaultMember = View | Join | Send | ViewUsers | ReadHistory,
        Moderator = DefaultMember | Invite | Kick | Mute,
        Owner = DefaultMember | Invite | Kick | Ban | Mute | ManageChannel,
        All = View | Join | Send | ViewUsers | Invite | Kick | Ban | Mute | ManageChannel | ReadHistory
    }

    public class ChatChannelOptions
    {
        public string Id { get; set; } = string.Empty;
        public ChatChannelType Type { get; set; } = ChatChannelType.Public;
        public string OwnerUsername { get; set; } = string.Empty;
        public string OwnerService { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsClosed { get; set; }
        public bool IsPersistent { get; set; }
        public bool IsArchived { get; set; }
        public int MaxHistory { get; set; } = 100;
        public ChatHistoryVisibility HistoryVisibility { get; set; } = ChatHistoryVisibility.MembersOnly;
        public ChatChannelPermission DefaultMemberPermissions { get; set; } = ChatChannelPermission.DefaultMember;
        public ChatChannelPermission InvitedMemberPermissions { get; set; } = ChatChannelPermission.DefaultMember;
        public ChatChannelPermission OwnerPermissions { get; set; } = ChatChannelPermission.Owner;

        public bool RequiresMembership()
        {
            return IsClosed || Type == ChatChannelType.PrivateGroup || Type == ChatChannelType.Direct || Type == ChatChannelType.System;
        }
    }
}
