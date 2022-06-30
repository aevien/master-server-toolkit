using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class NotificationHttpController : HttpController
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private NotificationModule notificationModule;

        #endregion

        public override void Initialize(HttpServerModule httpServer)
        {
            base.Initialize(httpServer);

            // Create new post controller
            httpServer.RegisterHttpRequestHandler("notify", HttpMethod.POST, OnNotifyHttpRequestHandler);
        }

        private void OnNotifyHttpRequestHandler(HttpListenerRequest request, HttpListenerResponse response)
        {
            var jsonResponse = new JObject();

            try
            {
                var jsonRequest = new JObject();

                using (StreamReader stream = new StreamReader(request.InputStream))
                {
                    jsonRequest = JObject.Parse(stream.ReadToEnd());
                }

                if (!jsonRequest.ContainsKey("message")) throw new Exception("[message] parameter is not defined");
                if (!notificationModule) throw new Exception("[NotificationModule] not found");

                // Read message
                string message = jsonRequest.Value<string>("message");

                // Send message to all recipients
                notificationModule.NoticeToAll(message, true);

                // Respond OK
                jsonResponse.Add("message", "ok");

                byte[] contents = Encoding.UTF8.GetBytes(jsonResponse.ToString());

                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;
                response.Close(contents, true);
            }
            catch (Exception e)
            {
                jsonResponse.Add("error", e.Message);

                byte[] contents = Encoding.UTF8.GetBytes(jsonResponse.ToString());

                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Close(contents, true);
            }
        }
    }
}