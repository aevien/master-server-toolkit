using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class BadRequest : StringResult
    {
        public BadRequest(string value = "") : base(value)
        {
            StatusCode = (int)HttpStatusCode.BadRequest;
        }
    }
}