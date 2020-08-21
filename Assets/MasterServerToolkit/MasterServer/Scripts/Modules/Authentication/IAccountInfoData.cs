using Aevien.Utilities;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents account data
    /// </summary>
    public interface IAccountInfoData
    {
        string Username { get; set; }
        string Password { get; set; }
        string Email { get; set; }
        string Token { get; set; }
        bool IsAdmin { get; set; }
        bool IsGuest { get; set; }
        bool IsEmailConfirmed { get; set; }
        Dictionary<string, string> Properties { get; set; }

        event Action<IAccountInfoData> OnChangedEvent;
        void MarkAsDirty();
        bool HasToken();
        bool IsTokenExpired();
    }
}