using MasterServerToolkit.Logging;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public class BinaryResult : HttpResult
    {
        public byte[] BinaryValue { get; set; }

        public BinaryResult(byte[] value, string contentType = "application/octet-stream")
        {
            BinaryValue = value;
            ContentType = contentType;
        }

        public override async Task Execute(HttpListenerContext context)
        {
            var response = context.Response;

            try
            {
                response.ContentLength64 = BinaryValue.LongLength;
                response.ContentType = ContentType;
                response.StatusCode = StatusCode;
                response.Headers = Headers;

                using (Stream output = response.OutputStream)
                {
                    await output.WriteAsync(BinaryValue, 0, BinaryValue.Length);
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
