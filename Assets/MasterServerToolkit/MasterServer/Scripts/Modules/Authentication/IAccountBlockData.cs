using MasterServerToolkit.Json;
using System;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Server-owned account block record. Account blocking is intentionally stored outside account data.
    /// </summary>
    public interface IAccountBlockData
    {
        string Id { get; set; }
        string AccountId { get; set; }
        string BlockReason { get; set; }
        DateTime BlockedUntil { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime? RemovedAt { get; set; }
        string RemoveReason { get; set; }

        bool IsActive();
        MstJson ToJson();
    }
}
