using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class BadRequestJson : JsonErrorResult
    {
        public BadRequestJson(string value = "") : base(HttpStatusCode.BadRequest, value, "bad_request")
        {
        }
    }
}
