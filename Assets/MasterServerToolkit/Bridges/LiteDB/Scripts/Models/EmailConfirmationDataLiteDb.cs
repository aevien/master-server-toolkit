using LiteDB;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class EmailConfirmationDataLiteDb
    {
        [BsonId]
        public string Email { get; set; }
        public string Code { get; set; }
    }
}