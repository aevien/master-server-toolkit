#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AccountBlockMongoDB : IAccountBlockData
    {
        [BsonId]
        public string Id { get; set; }
        public string AccountId { get; set; }
        public string BlockReason { get; set; }
        public DateTime BlockedUntil { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
        public string RemoveReason { get; set; }

        public AccountBlockMongoDB()
        {
            Id = Mst.Helper.CreateGuidString();
            AccountId = string.Empty;
            BlockReason = string.Empty;
            BlockedUntil = DateTime.MinValue;
            CreatedAt = DateTime.UtcNow;
            RemovedAt = null;
            RemoveReason = string.Empty;
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
#endif
