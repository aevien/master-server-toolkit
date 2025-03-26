using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class InternalServerError : StringResult
    {
        public InternalServerError(string value = "") : base(value)
        {
            StatusCode = (int)HttpStatusCode.InternalServerError;
        }
    }
}
