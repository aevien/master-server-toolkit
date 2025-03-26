using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class NotFound : StringResult
    {
        public NotFound(string value = "") : base(value)
        {
            StatusCode = (int)HttpStatusCode.NotFound;
        }
    }
}
