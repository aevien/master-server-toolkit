using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public class HttpResult : IHttpResult
    {
        public string Value { get; set; } = string.Empty;
        public string ContentType { get; set; } = "text/plain";
        public int StatusCode { get; set; } = (int)HttpStatusCode.OK;
        public WebHeaderCollection Headers { get; set; } = new WebHeaderCollection();

        public HttpResult(string value = "", string contentType = "text/plain")
        {
            Value = value;
            ContentType = contentType;
            Headers["Access-Control-Allow-Origin"] = "*";
        }

        public HttpResult(MstJson value, string contentType = "application/json") : this(value.ToString(), contentType) { }
        public HttpResult(object value, string contentType = "text/plain") : this(value.ToString(), contentType) { }

        public virtual async Task Execute(HttpListenerContext context)
        {
            var response = context.Response;

            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(Value);

                response.ContentLength64 = buffer.LongLength;
                response.ContentType = ContentType;
                response.StatusCode = StatusCode;
                response.Headers = Headers;

                using (Stream output = response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                Logs.Error(ex);
                response?.Abort();
            }
        }
    }
}
