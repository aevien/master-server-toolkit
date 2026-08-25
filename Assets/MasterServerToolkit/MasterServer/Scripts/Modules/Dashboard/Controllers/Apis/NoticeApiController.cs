using MasterServerToolkit.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public class NoticeApiController : WebController
    {
        private NotificationModule notificationModule;

        public override void Initialize(HttpServerModule webServer)
        {
            base.Initialize(webServer);

            notificationModule = Server.GetModule<NotificationModule>();

            if (notificationModule == null)
            {
                logger.Error($"This module requires the use of the {nameof(NotificationModule)}, please add it to the master server.");
                return;
            }

            webServer.RegisterPostHandler("api/v1/notification/user", NotifyUserHandler, UseCredentials);
            webServer.RegisterPostHandler("api/v1/notification/all", NotifyAllHandler, UseCredentials);
        }

        #region HANDLERS

        private bool TryReadJsonBody(HttpListenerRequest request, out MstJson data, out IHttpResult error)
        {
            data = MstJson.CreateObject();
            error = null;

            string body;

            using (StreamReader stream = new(request.InputStream))
            {
                body = stream.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                error = new BadRequestJson("Request body cannot be empty");
                return false;
            }

            try
            {
                data = new MstJson(body);
                return true;
            }
            catch (Exception)
            {
                error = new BadRequestJson("Request body must be valid JSON");
                return false;
            }
        }

        private IHttpResult CreateMissingNotificationModuleError()
        {
            return new InternalServerErrorJson($"{nameof(notificationModule)} not found");
        }

        private Task<IHttpResult> NotifyUserHandler(HttpListenerRequest request)
        {
            if (notificationModule == null)
                return Task.FromResult(CreateMissingNotificationModuleError());

            if (!TryReadJsonBody(request, out var data, out var error))
                return Task.FromResult(error);

            if (!data.HasField("id"))
                return Task.FromResult<IHttpResult>(new BadRequestJson("[id] parameter is not defined"));

            if (!data.HasField("message"))
                return Task.FromResult<IHttpResult>(new BadRequestJson("[message] parameter is not defined"));

            // Read message
            string id = data.GetField("id").StringValue;
            string message = data.GetField("message").StringValue?.Trim();

            if (string.IsNullOrEmpty(id))
                return Task.FromResult<IHttpResult>(new BadRequestJson("[id] parameter is empty"));

            if (string.IsNullOrEmpty(message))
                return Task.FromResult<IHttpResult>(new BadRequestJson("[message] parameter is empty"));

            if (!notificationModule.TryGetRecipient(id, out var recipient))
                return Task.FromResult<IHttpResult>(new NotFoundJson($"Recipient {id} not found or offline"));

            recipient.Notify(message);

            MstJson json = MstJson.CreateObject();
            json.AddField("sent", true);

            return Task.FromResult<IHttpResult>(new JsonResult(json));
        }

        private Task<IHttpResult> NotifyAllHandler(HttpListenerRequest request)
        {
            if (notificationModule == null)
                return Task.FromResult(CreateMissingNotificationModuleError());

            if (!TryReadJsonBody(request, out var data, out var error))
                return Task.FromResult(error);

            if (!data.HasField("message"))
                return Task.FromResult<IHttpResult>(new BadRequestJson("[message] parameter is not defined"));

            string message = data.GetField("message").StringValue?.Trim();

            if (string.IsNullOrEmpty(message))
                return Task.FromResult<IHttpResult>(new BadRequestJson("[message] parameter is empty"));

            bool remember = !data.HasField("remember") || data.GetField("remember").BoolValue;
            notificationModule.NoticeToAll(message, remember);

            MstJson json = MstJson.CreateObject();
            json.AddField("sent", true);
            json.AddField("remember", remember);

            return Task.FromResult<IHttpResult>(new JsonResult(json));
        }

        #endregion
    }
}
