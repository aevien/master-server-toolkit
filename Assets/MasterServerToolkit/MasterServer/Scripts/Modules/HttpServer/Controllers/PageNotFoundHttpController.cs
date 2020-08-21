using System.Text;
using WebSocketSharp.Net;

namespace MasterServerToolkit.MasterServer
{
    public class PageNotFoundHttpController : HttpController
    {
        public override void Initialize(HttpServerModule server)
        {
            base.Initialize(server);

            server.RegisterHttpRequestHandler("404", On404HttpRequestHandler);
        }

        private void On404HttpRequestHandler(HttpListenerRequest request, HttpListenerResponse response)
        {
            byte[] contents = GetHtmlBytes();

            response.ContentType = "text/html";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = contents.LongLength;
            response.Close(contents, true);
        }
    }
}
