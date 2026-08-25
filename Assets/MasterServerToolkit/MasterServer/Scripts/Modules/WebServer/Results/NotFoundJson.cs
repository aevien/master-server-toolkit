using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class NotFoundJson : JsonErrorResult
    {
        public NotFoundJson(string value = "Route not found") : base(HttpStatusCode.NotFound, value, "not_found")
        {
        }
    }
}
