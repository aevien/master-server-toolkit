using LiteDB;
using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AccountServiceBindingData : IAccountServiceBindingData
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
