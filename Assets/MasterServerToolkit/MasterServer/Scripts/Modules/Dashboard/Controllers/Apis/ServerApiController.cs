using MasterServerToolkit.Json;
using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public partial class ServerApiController : WebController
    {
        public override void Initialize(HttpServerModule webServer)
        {
            base.Initialize(webServer);

            // Info API
            webServer.RegisterGetHandler("api/v1/server/info", GetServerInfoHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/server/modules", GetModulesInfoHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/server/modules/id", GetModuleInfoByIdHandler, UseCredentials);
            webServer.RegisterGetHandler("api/v1/server/traffic", GetTrafficInfoHandler, UseCredentials);
        }

        #region HANDLERS

        private Task<IHttpResult> GetServerInfoHandler(HttpListenerRequest request)
        {
            return Task.FromResult<IHttpResult>(new JsonResult(Server.Info()));
        }

        private Task<IHttpResult> GetModulesInfoHandler(HttpListenerRequest Request)
        {
            MstJson json = MstJson.CreateObject();
            MstJson modulesJson = MstJson.CreateArray();

            foreach (var module in Server.GetInitializedModules())
            {
                modulesJson.Add(module.Info());
            }

            json.AddField("modules", modulesJson);

            var result = new JsonResult(json);
            return Task.FromResult<IHttpResult>(result);
        }

        private Task<IHttpResult> GetModuleInfoByIdHandler(HttpListenerRequest request)
        {
            string id = request.QueryString[MstParamKeys.QS_PARAM_ID];

            if (string.IsNullOrEmpty(id))
                return Task.FromResult<IHttpResult>(new BadRequestJson("Module id cannot be null. Use '?id=module-id-here' to get module by its id"));

            var module = Server.GetModuleById<IBaseServerModule>(id);

            if (module == null)
                return Task.FromResult<IHttpResult>(new NotFoundJson($"Module {id} not found"));

            return Task.FromResult<IHttpResult>(new JsonResult(module.Details()));
        }

        private Task<IHttpResult> GetTrafficInfoHandler(HttpListenerRequest request)
        {
            MstJson json = Mst.Traffic.Info();
            var result = new JsonResult(json);
            return Task.FromResult<IHttpResult>(result);
        }

        #endregion
    }
}
