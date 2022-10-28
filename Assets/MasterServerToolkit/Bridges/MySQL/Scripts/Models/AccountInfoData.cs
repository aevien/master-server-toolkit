using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.MySQL
{
    public class AccountInfoData : IAccountInfoData
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Token { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsGuest { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public DateTime LastLogin { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public event Action<IAccountInfoData> OnChangedEvent;

        public AccountInfoData()
        {
            Id = Mst.Helper.CreateGuidString();
            Username = string.Empty;
            Password = string.Empty;
            Email = string.Empty;
            PhoneNumber = string.Empty;
            Token = string.Empty;
            IsAdmin = false;
            IsGuest = true;
            IsEmailConfirmed = false;
            LastLogin = DateTime.UtcNow;
            Properties = new Dictionary<string, string>();
        }

        public void MarkAsDirty()
        {
            OnChangedEvent?.Invoke(this);
        }

        public override string ToString()
        {
            var properties = new MstProperties();
            properties.Set("Id", Id);
            properties.Set("Username", Username);
            properties.Set("Password", Password);
            properties.Set("Email", Email);
            properties.Set("PhoneNumber", PhoneNumber);
            properties.Set("Token", Token);
            properties.Set("IsAdmin", IsAdmin);
            properties.Set("IsGuest", IsGuest);
            properties.Set("IsEmailConfirmed", IsEmailConfirmed);
            properties.Set("DeviceId", DeviceId);
            properties.Set("DeviceName", DeviceName);
            properties.Set("LastLogin", LastLogin);
            properties.Append(Properties);
            return properties.ToString();
        }
    }
}