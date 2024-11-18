using MasterServerToolkit.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsHttpController : HttpController
    {
        public override void Initialize(HttpServerModule httpServer)
        {
            base.Initialize(httpServer);

            httpServer.RegisterHttpGetRequestHandler("analytics/by-user.json", OnGetAnalyticsJsonByUserIdHttpRequestHandler, UseCredentials);
        }

        private async void OnGetAnalyticsJsonByUserIdHttpRequestHandler(HttpListenerRequest request, HttpListenerResponse response)
        {
            string userId = request.QueryString["userId"];
            MstJson json = MstJson.EmptyArray;

            byte[] contents = new byte[0];
            var analyticsModule = HttpServer.Server.GetModule<AnalyticsModule>();

            if (string.IsNullOrEmpty(userId))
            {
                contents = Encoding.UTF8.GetBytes("User id cannot be empty");
                response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else if (analyticsModule == null)
            {
                contents = Encoding.UTF8.GetBytes("Analytics module not found");
                response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            else
            {
                foreach (var item in await analyticsModule.GetByUserId(userId, 0, 0))
                {
                    var data = MstJson.EmptyObject;
                    data.AddField("id", item.Id);
                    data.AddField("user_id", item.UserId);
                    data.AddField("event_id", item.EventId);
                    data.AddField("data", MstJson.Create(item.Data));
                    json.Add(data);
                }

                contents = Encoding.UTF8.GetBytes(json.ToString());
                response.StatusCode = (int)HttpStatusCode.OK;
            }

            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = contents.LongLength;
            response.Close(contents, true);
        }
    }
}
