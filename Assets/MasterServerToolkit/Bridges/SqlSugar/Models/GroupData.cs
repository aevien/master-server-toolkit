using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.Groups)]
    public class GroupData
    {
        [SugarColumn(ColumnName = "id", Length = 38, IsPrimaryKey = true, IsNullable = false)]
        public string Id { get; set; }

        [SugarColumn(ColumnName = "name", Length = 64, IsNullable = false)]
        public string Name { get; set; }

        [SugarColumn(ColumnName = "normalized_name", Length = 64, IsNullable = false)]
        public string NormalizedName { get; set; }

        [SugarColumn(ColumnName = "tag", Length = 12, IsNullable = false)]
        public string Tag { get; set; }

        [SugarColumn(ColumnName = "normalized_tag", Length = 12, IsNullable = false)]
        public string NormalizedTag { get; set; }

        [SugarColumn(ColumnName = "description", Length = 512, IsNullable = true)]
        public string Description { get; set; }

        [SugarColumn(ColumnName = "owner_account_id", Length = 38, IsNullable = false)]
        public string OwnerAccountId { get; set; }

        [SugarColumn(ColumnName = "owner_username", Length = 64, IsNullable = false)]
        public string OwnerUsername { get; set; }

        [SugarColumn(ColumnName = "chat_channel_name", Length = 32, IsNullable = false)]
        public string ChatChannelName { get; set; }

        [SugarColumn(ColumnName = "join_policy", IsNullable = false)]
        public int JoinPolicy { get; set; }

        [SugarColumn(ColumnName = "max_members", IsNullable = false)]
        public int MaxMembers { get; set; }

        [SugarColumn(ColumnName = "created_at", IsNullable = false)]
        public DateTime CreatedAtUtc { get; set; }

        [SugarColumn(ColumnName = "updated_at", IsNullable = false)]
        public DateTime UpdatedAtUtc { get; set; }

        public MstGroupInfo ToInfo()
        {
            return new MstGroupInfo
            {
                Id = Id ?? string.Empty,
                Name = Name ?? string.Empty,
                Tag = Tag ?? string.Empty,
                Description = Description ?? string.Empty,
                OwnerAccountId = OwnerAccountId ?? string.Empty,
                OwnerUsername = OwnerUsername ?? string.Empty,
                ChatChannelName = ChatChannelName ?? string.Empty,
                JoinPolicy = (MstGroupJoinPolicy)JoinPolicy,
                MaxMembers = MaxMembers,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = UpdatedAtUtc
            };
        }

        public static GroupData FromInfo(MstGroupInfo info)
        {
            return new GroupData
            {
                Id = info.Id,
                Name = info.Name,
                NormalizedName = NormalizeKey(info.Name),
                Tag = info.Tag,
                NormalizedTag = NormalizeKey(info.Tag),
                Description = info.Description,
                OwnerAccountId = info.OwnerAccountId,
                OwnerUsername = info.OwnerUsername,
                ChatChannelName = info.ChatChannelName,
                JoinPolicy = (int)info.JoinPolicy,
                MaxMembers = info.MaxMembers,
                CreatedAtUtc = info.CreatedAtUtc,
                UpdatedAtUtc = info.UpdatedAtUtc
            };
        }

        public static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
