using MasterServerToolkit.Json;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents account data
    /// </summary>
    public interface IAccountInfoData
    {
        string Id { get; set; }
        string Username { get; set; }
        string Password { get; set; }
        string Email { get; set; }
        string Token { get; set; }
        DateTime LastLoginAt { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime UpdatedAt { get; set; }
        bool IsAdmin { get; set; }
        bool IsGuest { get; set; }
        bool IsEmailConfirmed { get; set; }
        /// <summary>
        /// Client-writable account metadata.
        /// Do not use these values as authoritative gameplay, economy, permission, or entitlement state.
        /// Use server-owned profile properties or a server-only table for data that requires server authorization.
        /// Mutating this dictionary is not a persistence operation by itself; server code must save the account
        /// through the accounts database accessor or an authorized account update handler.
        /// </summary>
        Dictionary<string, string> ExtraProperties { get; set; }

        event Action<IAccountInfoData> OnChangedEvent;
        void MarkAsDirty();
        MstJson ToJson();
    }
}
