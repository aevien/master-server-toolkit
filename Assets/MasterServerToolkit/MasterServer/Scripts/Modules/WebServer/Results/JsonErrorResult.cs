using MasterServerToolkit.Json;
using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class JsonErrorResult : JsonResult
    {
        public JsonErrorResult(HttpStatusCode statusCode, string message, string code = null)
            : this((int)statusCode, message, code)
        {
        }

        public JsonErrorResult(int statusCode, string message, string code = null)
            : base(CreateJson(statusCode, message, code))
        {
            StatusCode = statusCode;
        }

        private static MstJson CreateJson(int statusCode, string message, string code)
        {
            var json = MstJson.CreateObject();
            var error = MstJson.CreateObject();

            error.AddField("status", statusCode);
            error.AddField("code", string.IsNullOrEmpty(code) ? GetDefaultCode(statusCode) : code);
            error.AddField("message", message ?? string.Empty);

            json.AddField("error", error);
            return json;
        }

        private static string GetDefaultCode(int statusCode)
        {
            if (statusCode == (int)HttpStatusCode.BadRequest)
                return "bad_request";

            if (statusCode == (int)HttpStatusCode.Unauthorized)
                return "unauthorized";

            if (statusCode == (int)HttpStatusCode.NotFound)
                return "not_found";

            if (statusCode == (int)HttpStatusCode.InternalServerError)
                return "internal_server_error";

            return "error";
        }
    }
}
