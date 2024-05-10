using LiteDB;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ProfileInfoData
    {
        [BsonId]
        public string UserId { get; set; }
        public byte[] Data { get; set; }
    }
}