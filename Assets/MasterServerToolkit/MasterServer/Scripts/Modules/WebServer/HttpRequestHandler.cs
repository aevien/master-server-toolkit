using MasterServerToolkit.Json;
using System;
using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public delegate Task<IHttpResult> HttpResultHandler(HttpListenerRequest request);

    public class HttpRequestHandler
    {
        public string Api {  get; set; }
        public bool UseCredentials { get; set; }
        public HttpResultHandler Action { get; set; }
        public MstJson Extra { get; set; }

        public HttpRequestHandler(string api, bool useCredentials, HttpResultHandler action, MstJson extra)
        {
            Api = api ?? throw new ArgumentNullException(nameof(api));
            UseCredentials = useCredentials;
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Extra = extra ?? MstJson.EmptyObject;
        }

        public MstJson ToJson()
        {
            var json = MstJson.EmptyObject;
            json.AddField("api", Api);
            json.AddField("use_credentials", UseCredentials);
            json.AddField("extra", Extra);
            return json;
        }
    }
}
