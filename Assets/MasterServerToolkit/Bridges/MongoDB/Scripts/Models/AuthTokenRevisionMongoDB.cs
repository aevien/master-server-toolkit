#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MongoDB.Bson.Serialization.Attributes;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AuthTokenRevisionMongoDB
    {
        [BsonId]
        public string AccountId { get; set; }

        public int Revision { get; set; }
    }
}
#endif
