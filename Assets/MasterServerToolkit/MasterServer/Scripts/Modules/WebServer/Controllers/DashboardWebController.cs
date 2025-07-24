using MasterServerToolkit.Json;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class DashboardWebController : TemplateWebController
    {
        [SerializeField]
        private TextAsset pageAsset;

        private string page;

        public override void Initialize(WebServerModule webServer)
        {
            page = pageAsset.text;

            base.Initialize(webServer);
            webServer.RegisterGetHandler("dashboard", GetServerInfoPageHandler, UseCredentials);
            webServer.RegisterGetHandler("info/data", GetServerInfoHandler, UseCredentials);
        }

        #region HANDLERS

        private Task<IHttpResult> GetServerInfoPageHandler(HttpListenerRequest request)
        {
            var result = new HtmlResult(Combine(page));
            return Task.FromResult<IHttpResult>(result);
        }

        private Task<IHttpResult> GetServerInfoHandler(HttpListenerRequest Request)
        {
            MstJson json = MstJson.EmptyObject;
            MstJson modulesJson = MstJson.EmptyArray;

            foreach (var module in MasterServer.GetInitializedModules())
            {
                modulesJson.Add(module.JsonInfo());
            }

            json.AddField("modules", modulesJson);

            var result = new JsonResult(json);
            result.ConfigureCors();
            return Task.FromResult<IHttpResult>(result);
        }

        #endregion
    }
}
