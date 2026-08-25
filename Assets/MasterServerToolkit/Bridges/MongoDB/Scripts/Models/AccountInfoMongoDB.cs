#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AccountInfoMongoDB : IAccountInfoData
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public string Id { get => _id.ToString(); set => _id = new ObjectId(value); }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Token { get; set; }
        public DateTime LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsGuest { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public Dictionary<string, string> ExtraProperties { get; set; }

        public event Action<IAccountInfoData> OnChangedEvent;

        public AccountInfoMongoDB()
        {
            Id = Mst.Helper.CreateID_10();
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
            ExtraProperties = new Dictionary<string, string>()
            {
                { "phone_number", string.Empty },
                { "facebook_id", string.Empty },
                { "google_play_id", string.Empty },
                { "yandex_games_id", string.Empty },
            };
        }

        public void MarkAsDirty()
        {
            OnChangedEvent?.Invoke(this);
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
#endif
