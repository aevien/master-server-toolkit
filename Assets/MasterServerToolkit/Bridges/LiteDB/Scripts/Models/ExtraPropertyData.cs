using LiteDB;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ExtraPropertyData
    {
        [BsonId]
        public string Id { get; set; }
        public string AccountId { get; set; }
        public string PropertyKey { get; set; }
        public string PropertyValue { get; set; }
    }
}