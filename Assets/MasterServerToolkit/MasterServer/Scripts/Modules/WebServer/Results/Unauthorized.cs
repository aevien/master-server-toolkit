using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class Unauthorized : StringResult
    {
        public Unauthorized(string value = "Invalid login or password", string realm = null) : base(value)
        {
            if (!string.IsNullOrEmpty(realm))
                Headers["WWW-Authenticate"] = $"Basic realm=\"{realm}\", charset=\"UTF-8\"";

            StatusCode = (int)HttpStatusCode.Unauthorized;
        }
    }
}