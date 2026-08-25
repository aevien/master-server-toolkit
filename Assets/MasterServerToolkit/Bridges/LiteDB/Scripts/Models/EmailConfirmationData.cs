using LiteDB;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class EmailConfirmationData
    {
        [BsonId]
        public string Email { get; set; }
        public string Code { get; set; }
        public System.DateTime ExpiresAt { get; set; }
        public int AttemptsLeft { get; set; }
    }
}
