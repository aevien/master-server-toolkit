using LiteDB;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ProfileInfoData
    {
        [BsonId]
        public string Username { get; set; }
        public byte[] Data { get; set; }
    }
}
