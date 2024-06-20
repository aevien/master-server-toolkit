#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class ProfileInfoDocumentMongoDB
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public string Id { get => _id.ToString(); set => _id = new ObjectId(value); }

        public string UserId { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.Document)]
        public Dictionary<string, string> Document { get; set; }
    }
}
#endif