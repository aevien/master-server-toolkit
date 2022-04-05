using LiteDB;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class EmailConfirmationData
    {
        [BsonId]
        public string Email { get; set; }
        public string Code { get; set; }
    }
}