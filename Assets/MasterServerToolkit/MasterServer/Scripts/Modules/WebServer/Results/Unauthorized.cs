using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class Unauthorized : StringResult
    {
        public Unauthorized(string value = "Invalid login or password") : base(value)
        {
            StatusCode = (int)HttpStatusCode.Unauthorized;
        }
    }
}
