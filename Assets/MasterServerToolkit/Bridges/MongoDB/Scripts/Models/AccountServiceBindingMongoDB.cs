#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.MasterServer;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AccountServiceBindingMongoDB : IAccountServiceBindingData
    {
        [BsonId]
        public string Id { get; set; }
        public string AccountId { get; set; }
        public string ServiceId { get; set; }
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public bool IsGuest { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
    }
}
#endif
