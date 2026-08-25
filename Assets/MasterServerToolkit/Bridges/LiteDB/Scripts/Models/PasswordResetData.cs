using LiteDB;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class PasswordResetData
    {
        [BsonId]
        public string Email { get; set; }
        public string Code { get; set; }
        public System.DateTime ExpiresAt { get; set; }
        public int AttemptsLeft { get; set; }
    }
}
