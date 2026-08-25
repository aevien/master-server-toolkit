using System;

namespace MasterServerToolkit.MasterServer
{
    public enum MstGroupRole : byte
    {
        Member = 0,
        Officer = 1,
        Owner = 2
    }

    public enum MstGroupJoinPolicy : byte
    {
        InviteOnly = 0,
        Open = 1,
        Request = 2
    }

    public enum MstGroupInviteStatus : byte
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,
        Revoked = 3,
        Expired = 4
    }

    public class MstGroupInfo
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
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public class MstGroupMemberInfo
    {
        public string GroupId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public MstGroupRole Role { get; set; } = MstGroupRole.Member;
        public DateTime JoinedAtUtc { get; set; }
        public DateTime LastSeenAtUtc { get; set; }
    }

    public class MstGroupInviteInfo
    {
        public string Id { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string FromAccountId { get; set; } = string.Empty;
        public string FromUsername { get; set; } = string.Empty;
        public string ToAccountId { get; set; } = string.Empty;
        public string ToUsername { get; set; } = string.Empty;
        public MstGroupInviteStatus Status { get; set; } = MstGroupInviteStatus.Pending;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
