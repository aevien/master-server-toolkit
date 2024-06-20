#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
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
        public DateTime LastLogin { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsGuest { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public bool IsBanned { get; set; }
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
            IsBanned = false;
            LastLogin = DateTime.UtcNow;
            Created = DateTime.UtcNow;
            Updated = DateTime.UtcNow;
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
    }
}
#endif