#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class EmailConfirmationDataMongoDB
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public string Id { get => _id.ToString(); set => _id = new ObjectId(value); }
        public string Email { get; set; }
        public string Code { get; set; }
    }
}
#endif