using System.Text;
using UnityEngine;
using WebSocketSharp.Net;

namespace MasterServerToolkit.MasterServer
{
    public class MstInfoHttpController : HttpController
    {
        public override void Initialize(HttpServerModule server)
        {
            base.Initialize(server);
            server.RegisterHttpRequestHandler("info", OnGetMstInfoHttpRequestHandler);
        }

        private void OnGetMstInfoHttpRequestHandler(HttpListenerRequest request, HttpListenerResponse response)
        {
            //Mst.Concurrency.RunInMainThread(() => {
            //    // Here you can call unity API
            //    //Debug.Log(request.Headers);
            //});

            byte[] contents = GetResponseBytes();

            response.ContentType = "text/html";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = contents.LongLength;
            response.Close(contents, true);
        }
    }
}