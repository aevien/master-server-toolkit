using MasterServerToolkit.Json;
using MasterServerToolkit.Utils;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class NotificationWebController : WebController
    {
        [SerializeField]
        private HelpBox help = new HelpBox()
        {
            Text = "Use {\"message\":\"some message\"} in your post request body to test it"
        };
        private NotificationModule notificationModule;

        public override void Initialize(WebServerModule webServer)
        {
            base.Initialize(webServer);

            notificationModule = MasterServer.GetModule<NotificationModule>();

            if (notificationModule == null)
            {
                logger.Error($"This module requires the use of the {nameof(NotificationModule)}, please add it to the master server.");
                return;
            }

            webServer.RegisterPostHandler("notify", OnNotifyRequestHandler, UseCredentials);
        }

        #region HANDLERS

        private Task<IHttpResult> OnNotifyRequestHandler(HttpListenerRequest request)
        {
            var jsonRequest = new MstJson();

            using (StreamReader stream = new StreamReader(request.InputStream))
            {
                jsonRequest = new MstJson(stream.ReadToEnd());
            }

            if (!jsonRequest.HasField("message")) 
                return Task.FromResult<IHttpResult>(new BadRequest("[message] parameter is not defined"));

            // Read message
            string message = jsonRequest.GetField("message").StringValue;

            // Send message to all recipients
            notificationModule.NoticeToAll(message, true);
            return Task.FromResult<IHttpResult>(new HttpResult());
        }

        #endregion
    }
}
