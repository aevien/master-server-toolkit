using LiteDB;
using MasterServerToolkit.MasterServer;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class PasswordResetData
    {
        [BsonId]
        public string Email { get; set; }
        public string Code { get; set; }
    }
}