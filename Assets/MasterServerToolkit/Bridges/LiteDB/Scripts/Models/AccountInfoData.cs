using LiteDB;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AccountInfoData : IAccountInfoData
    {
        [BsonId]
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
        public DateTime LastLogin { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsGuest { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public bool IsBanned { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        [BsonIgnore]
        public Dictionary<string, string> ExtraProperties { get; set; }

        public event Action<IAccountInfoData> OnChangedEvent;

        public AccountInfoData()
        {
            Id = Mst.Helper.CreateGuidString();
            Username = string.Empty;
            Password = string.Empty;
            Email = string.Empty;
            Token = string.Empty;
            IsAdmin = false;
            IsGuest = true;
            IsEmailConfirmed = false;
            IsBanned = false;
            LastLogin = DateTime.UtcNow;
            Created = DateTime.UtcNow;
            Updated = DateTime.UtcNow;
            ExtraProperties = new Dictionary<string, string>();
        }

        public void MarkAsDirty()
        {
            OnChangedEvent?.Invoke(this);
        }
    }
}