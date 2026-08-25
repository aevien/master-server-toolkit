using MasterServerToolkit.Json;
using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public class RoomsApiController : WebController
    {
        private RoomsModule roomsModule;

        public override void Initialize(HttpServerModule webServer)
        {
            base.Initialize(webServer);

            roomsModule = Server.GetModule<RoomsModule>();

            // Info API
            webServer.RegisterGetHandler("api/v1/rooms/info", GetRoomsInfoHandler, UseCredentials);
        }

        #region HANDLERS

        private Task<IHttpResult> GetRoomsInfoHandler(HttpListenerRequest request)
        {
            MstJson json = MstJson.CreateObject();

            if (roomsModule != null)
                json = roomsModule.Details();

            return Task.FromResult<IHttpResult>(new JsonResult(json));
        }

        #endregion
    }
}