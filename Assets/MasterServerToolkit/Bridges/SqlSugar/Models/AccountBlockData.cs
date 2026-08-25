using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.AccountBlocks)]
    public class AccountBlockData : IAccountBlockData
    {
        [SugarColumn(ColumnName = "id", Length = 38, IsPrimaryKey = true)]
        public string Id { get; set; }

        [SugarColumn(ColumnName = "account_id", Length = 38)]
        public string AccountId { get; set; }

        [SugarColumn(ColumnName = "block_reason", Length = 1024, IsNullable = true)]
        public string BlockReason { get; set; }

        [SugarColumn(ColumnName = "blocked_until")]
        public DateTime BlockedUntil { get; set; }

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; }

        [SugarColumn(ColumnName = "removed_at", IsNullable = true)]
        public DateTime? RemovedAt { get; set; }

        [SugarColumn(ColumnName = "remove_reason", Length = 1024, IsNullable = true)]
        public string RemoveReason { get; set; }

        public AccountBlockData()
        {
            Id = Mst.Helper.CreateGuidString();
            AccountId = string.Empty;
            BlockReason = string.Empty;
            BlockedUntil = DateTime.MinValue;
            CreatedAt = DateTime.UtcNow;
            RemovedAt = null;
            RemoveReason = string.Empty;
        }

        public AccountBlockData(IAccountBlockData data)
        {
            Id = data.Id;
            AccountId = data.AccountId;
            BlockReason = data.BlockReason;
            BlockedUntil = data.BlockedUntil;
            CreatedAt = data.CreatedAt;
            RemovedAt = data.RemovedAt;
            RemoveReason = data.RemoveReason;
        }

        public bool IsActive()
        {
            return !RemovedAt.HasValue && BlockedUntil > DateTime.UtcNow;
        }

        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.AddField("id", Id);
            json.AddField("accountId", AccountId);
            json.AddField("blockReason", BlockReason);
            json.AddField("blockedUntil", BlockedUntil.ToString("o"));
            json.AddField("createdAt", CreatedAt.ToString("o"));
            json.AddField("removedAt", RemovedAt.HasValue ? RemovedAt.Value.ToString("o") : string.Empty);
            json.AddField("removeReason", RemoveReason);
            json.AddField("isActive", IsActive());
            return json;
        }
    }
}
