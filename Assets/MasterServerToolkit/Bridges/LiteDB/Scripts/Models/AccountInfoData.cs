using LiteDB;
using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AccountInfoData : IAccountInfoData, IEquatable<AccountInfoData>
    {
        [BsonId]
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
        public DateTime LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsGuest { get; set; }
        public bool IsEmailConfirmed { get; set; }
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
            LastLoginAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            ExtraProperties = new Dictionary<string, string>();
        }

        public void MarkAsDirty()
        {
            OnChangedEvent?.Invoke(this);
        }

        public bool Equals(AccountInfoData other)
        {
            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.AddField("id", Id);
            json.AddField("username", Username);
            json.AddField("email", Email);
            json.AddField("lastLoginAt", LastLoginAt);
            json.AddField("createdAt", CreatedAt);
            json.AddField("updatedAt", UpdatedAt);
            json.AddField("isAdmin", IsAdmin);
            json.AddField("isGuest", IsGuest);
            json.AddField("isEmailConfirmed", IsEmailConfirmed);
            json.AddField("extras", MstJson.Create(ExtraProperties));
            return json;
        }
    }
}
