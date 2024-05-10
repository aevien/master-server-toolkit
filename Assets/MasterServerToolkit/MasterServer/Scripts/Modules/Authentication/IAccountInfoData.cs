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
        DateTime LastLogin { get; set; }
        DateTime Created { get; set; }
        DateTime Updated { get; set; }
        bool IsAdmin { get; set; }
        bool IsGuest { get; set; }
        bool IsEmailConfirmed { get; set; }
        bool IsBanned { get; set; }
        string DeviceId { get; set; }
        string DeviceName { get; set; }
        Dictionary<string, string> ExtraProperties { get; set; }

        event Action<IAccountInfoData> OnChangedEvent;
        void MarkAsDirty();
    }
}