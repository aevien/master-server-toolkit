using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.GroupInvites)]
    public class GroupInviteData
    {
        [SugarColumn(ColumnName = "id", Length = 38, IsPrimaryKey = true, IsNullable = false)]
        public string Id { get; set; }

        [SugarColumn(ColumnName = "group_id", Length = 38, IsNullable = false)]
        public string GroupId { get; set; }

        [SugarColumn(ColumnName = "from_account_id", Length = 38, IsNullable = false)]
        public string FromAccountId { get; set; }

        [SugarColumn(ColumnName = "from_username", Length = 64, IsNullable = false)]
        public string FromUsername { get; set; }

        [SugarColumn(ColumnName = "to_account_id", Length = 38, IsNullable = false)]
        public string ToAccountId { get; set; }

        [SugarColumn(ColumnName = "to_username", Length = 64, IsNullable = false)]
        public string ToUsername { get; set; }

        [SugarColumn(ColumnName = "status", IsNullable = false)]
        public int Status { get; set; }

        [SugarColumn(ColumnName = "created_at", IsNullable = false)]
        public DateTime CreatedAtUtc { get; set; }

        [SugarColumn(ColumnName = "expires_at", IsNullable = false)]
        public DateTime ExpiresAtUtc { get; set; }

        [SugarColumn(ColumnName = "updated_at", IsNullable = false)]
        public DateTime UpdatedAtUtc { get; set; }

        public MstGroupInviteInfo ToInfo()
        {
            return new MstGroupInviteInfo
            {
                Id = Id ?? string.Empty,
                GroupId = GroupId ?? string.Empty,
                FromAccountId = FromAccountId ?? string.Empty,
                FromUsername = FromUsername ?? string.Empty,
                ToAccountId = ToAccountId ?? string.Empty,
                ToUsername = ToUsername ?? string.Empty,
                Status = (MstGroupInviteStatus)Status,
                CreatedAtUtc = CreatedAtUtc,
                ExpiresAtUtc = ExpiresAtUtc,
                UpdatedAtUtc = UpdatedAtUtc
            };
        }

        public static GroupInviteData FromInfo(MstGroupInviteInfo info)
        {
            return new GroupInviteData
            {
                Id = info.Id,
                GroupId = info.GroupId,
                FromAccountId = info.FromAccountId,
                FromUsername = info.FromUsername,
                ToAccountId = info.ToAccountId,
                ToUsername = info.ToUsername,
                Status = (int)info.Status,
                CreatedAtUtc = info.CreatedAtUtc,
                ExpiresAtUtc = info.ExpiresAtUtc,
                UpdatedAtUtc = info.UpdatedAtUtc
            };
        }
    }
}
