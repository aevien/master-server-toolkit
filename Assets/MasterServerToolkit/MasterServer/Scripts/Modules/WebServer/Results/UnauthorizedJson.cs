using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class UnauthorizedJson : JsonErrorResult
    {
        public UnauthorizedJson(string value = "Invalid login or password", string realm = null)
            : base(HttpStatusCode.Unauthorized, value, "unauthorized")
        {
            if (!string.IsNullOrEmpty(realm))
                Headers["WWW-Authenticate"] = $"Basic realm=\"{realm}\", charset=\"UTF-8\"";
        }
    }
}
