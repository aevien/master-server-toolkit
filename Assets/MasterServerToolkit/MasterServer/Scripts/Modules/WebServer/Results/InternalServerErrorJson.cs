using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class InternalServerErrorJson : JsonErrorResult
    {
        public InternalServerErrorJson(string value = "")
            : base(HttpStatusCode.InternalServerError, value, "internal_server_error")
        {
        }
    }
}
