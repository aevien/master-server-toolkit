using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.GroupMembers)]
    public class GroupMemberData
    {
        [SugarColumn(ColumnName = "group_id", Length = 38, IsPrimaryKey = true, IsNullable = false)]
        public string GroupId { get; set; }

        [SugarColumn(ColumnName = "account_id", Length = 38, IsPrimaryKey = true, IsNullable = false)]
        public string AccountId { get; set; }

        [SugarColumn(ColumnName = "username", Length = 64, IsNullable = false)]
        public string Username { get; set; }

        [SugarColumn(ColumnName = "role", IsNullable = false)]
        public int Role { get; set; }

        [SugarColumn(ColumnName = "joined_at", IsNullable = false)]
        public DateTime JoinedAtUtc { get; set; }

        [SugarColumn(ColumnName = "last_seen_at", IsNullable = false)]
        public DateTime LastSeenAtUtc { get; set; }

        public MstGroupMemberInfo ToInfo()
        {
            return new MstGroupMemberInfo
            {
                GroupId = GroupId ?? string.Empty,
                AccountId = AccountId ?? string.Empty,
                Username = Username ?? string.Empty,
                Role = (MstGroupRole)Role,
                JoinedAtUtc = JoinedAtUtc,
                LastSeenAtUtc = LastSeenAtUtc
            };
        }

        public static GroupMemberData FromInfo(MstGroupMemberInfo info)
        {
            return new GroupMemberData
            {
                GroupId = info.GroupId,
                AccountId = info.AccountId,
                Username = info.Username,
                Role = (int)info.Role,
                JoinedAtUtc = info.JoinedAtUtc,
                LastSeenAtUtc = info.LastSeenAtUtc
            };
        }
    }
}
