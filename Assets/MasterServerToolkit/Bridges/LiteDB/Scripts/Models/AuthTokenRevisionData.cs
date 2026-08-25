using LiteDB;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AuthTokenRevisionData
    {
        [BsonId]
        public string AccountId { get; set; }
        public int Revision { get; set; }
    }
}
