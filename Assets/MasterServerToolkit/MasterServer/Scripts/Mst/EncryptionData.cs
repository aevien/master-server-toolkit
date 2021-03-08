using System.Security.Cryptography;

namespace MasterServerToolkit.MasterServer
{
    public partial class MstSecurity
    {
        private class EncryptionData
        {
            public string ClientAesKey { get; set; }
            public RSACryptoServiceProvider ClientsCsp { get; set; }
            public RSAParameters ClientsPublicKey { get; set; }
        }
    }
}