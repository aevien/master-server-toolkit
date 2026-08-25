using System.Net;

namespace MasterServerToolkit.MasterServer
{
    public class NoContentResult : StringResult
    {
        public NoContentResult() : base(string.Empty)
        {
            StatusCode = (int)HttpStatusCode.NoContent;
            Headers.Add(HttpRequestHeader.ContentLength, "0");
        }
    }
}
