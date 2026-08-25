using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class NotFound : StringResult
    {
        public NotFound(string value = "Route not found") : base(value)
        {
            StatusCode = (int)HttpStatusCode.NotFound;
        }
    }
}