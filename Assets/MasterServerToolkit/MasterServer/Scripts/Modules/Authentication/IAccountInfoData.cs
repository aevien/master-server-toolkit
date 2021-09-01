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
        string PhoneNumber { get; set; }
        string Facebook { get; set; }
        string Google { get; set; }
        string Apple { get; set; }
        string Token { get; set; }
        DateTime LastLogin { get; set; }
        bool IsAdmin { get; set; }
        bool IsGuest { get; set; }
        string DeviceId { get; set; }
        string DeviceName { get; set; }
        bool IsEmailConfirmed { get; set; }
        Dictionary<string, string> Properties { get; set; }

        event Action<IAccountInfoData> OnChangedEvent;
        void MarkAsDirty();
        bool HasToken();
        bool IsTokenExpired();
        void SetToken(int days);
    }
}