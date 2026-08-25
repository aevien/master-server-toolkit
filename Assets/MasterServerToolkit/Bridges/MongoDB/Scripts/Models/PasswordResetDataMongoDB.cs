#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class PasswordResetDataMongoDB
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public string Id { get => _id.ToString(); set => _id = new ObjectId(value); }

        public string Email { get; set; }
        public string Code { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime ExpiresAt { get; set; }

        public int AttemptsLeft { get; set; }
    }
}
#endif
