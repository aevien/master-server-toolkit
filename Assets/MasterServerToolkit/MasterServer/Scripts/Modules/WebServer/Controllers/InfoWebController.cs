using MasterServerToolkit.Json;
using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public class InfoWebController : WebController
    {
        public override void Initialize(WebServerModule webServer)
        {
            base.Initialize(webServer);

            webServer.RegisterGetHandler("info.json", GetServerInfoHandler, UseCredentials);
        }

        #region HANDLERS

        private Task<IHttpResult> GetServerInfoHandler(HttpListenerRequest Request)
        {
            MstJson json = MstJson.EmptyObject;
            MstJson modulesJson = MstJson.EmptyArray;

            foreach (var module in MasterServer.GetInitializedModules())
            {
                modulesJson.Add(module.JsonInfo());
            }

            json.AddField("modules", modulesJson);

            return Task.FromResult<IHttpResult>(new JsonResult(json));
        }

        #endregion
    }
}
