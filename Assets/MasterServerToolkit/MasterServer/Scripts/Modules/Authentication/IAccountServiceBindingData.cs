using System;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Server-owned binding between one MST account and one external game-service identity.
    /// Use this for authoritative login lookup instead of client-controlled extra properties.
    /// </summary>
    public interface IAccountServiceBindingData
    {
        string Id { get; set; }
        string AccountId { get; set; }
        string ServiceId { get; set; }
        string PlayerId { get; set; }
        string PlayerName { get; set; }
        bool IsGuest { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime UpdatedAt { get; set; }
        DateTime LastLoginAt { get; set; }
    }
}
