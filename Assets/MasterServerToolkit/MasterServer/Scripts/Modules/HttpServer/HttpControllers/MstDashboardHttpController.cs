using MasterServerToolkit.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MstDashboardHttpController : HttpController
    {
        #region INSPECTOR

        [Header("Modules"), SerializeField]
        private DashboardModule dashboardModule;

        [Header("Application"), SerializeField]
        private TextAsset appHtmlFile;
        [SerializeField]
        private TextAsset appVueFile;

        [Header("System"), SerializeField]
        private MstDashboardComponent[] appComponents;

        [Header("Mst"), SerializeField]
        private Texture2D logoImage;

        #endregion

        private string appHtml;
        private string appVue;

        private readonly Dictionary<string, string> components = new Dictionary<string, string>();

        private const string MST_TITLE = "#mstTitle";
        private const string MST_VERSION = "#mstVersion";
        private const string MST_LOGO = "#mstLogo";
        private const string JS_OUTPUT = "#jsOutput";
        private const string VUE_COMPONENT_TEMPLATE = "#vueComponentTemplate";

        private byte[] logoRawData;

        private void Start()
        {
            appHtml = appHtmlFile.text;
            appVue = appVueFile.text;

            logoRawData = logoImage.EncodeToPNG();

            foreach (var appComponent in appComponents)
            {
                if (!components.ContainsKey(appComponent.name))
                {
                    string htmlTemplate = appComponent.htmlTemplate.text;
                    string vueScript = appComponent.vueScript.text;
                    Inject(htmlTemplate, vueScript, VUE_COMPONENT_TEMPLATE, out string result);
                    components.Add(appComponent.name, result);
                }
            }
        }

        public override void Initialize(HttpServerModule httpServer)
        {
            base.Initialize(httpServer);

            httpServer.RegisterHttpGetRequestHandler("dashboard", OnDashboardRequestHandlerAsync);
            httpServer.RegisterHttpGetRequestHandler("get-system-info", OnGetSystemInfoRequestHandlerAsync);
            httpServer.RegisterHttpGetRequestHandler("get-server-info", OnGetServerInfoRequestHandlerAsync);
            httpServer.RegisterHttpGetRequestHandler("get-modules-info", OnGetModulesInfoRequestHandlerAsync);
        }

        public override void Dispose()
        {
            base.Dispose();
            logoRawData = new byte[0];
            components.Clear();
        }

        private void Inject(string source, string target, string place, out string result)
        {
            result = target.Replace(place, source);
        }

        #region POST

        #endregion

        #region GET

        private async void OnDashboardRequestHandlerAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                foreach (var component in components)
                {
                    Inject(component.Value, appVue, component.Key, out appVue);
                }

                // Mount app js
                Inject(appVue, appHtml, JS_OUTPUT, out appHtml);

                // Mount MST info
                Inject(Mst.Name, appHtml, MST_TITLE, out appHtml);
                Inject(Mst.Version, appHtml, MST_VERSION, out appHtml);

                string logoBase64 = $"data:image/png;base64,{Convert.ToBase64String(logoRawData)}";
                Inject(logoBase64, appHtml, MST_LOGO, out appHtml);

                byte[] contents = Encoding.UTF8.GetBytes(appHtml);

                response.ContentType = "text/html";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;

                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(contents, 0, contents.Length);
                    stream.Close();
                }

                response.Close();
            }
            catch (Exception e)
            {
                byte[] contents = Encoding.UTF8.GetBytes(e.ToString());

                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.ContentType = "text/html";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;

                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(contents, 0, contents.Length);
                    stream.Close();
                }
            }
        }

        private async void OnGetServerInfoRequestHandlerAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                MstJson json = new MstJson();

                foreach (var kvp in dashboardModule.ServerInfo)
                {
                    json.AddField(kvp.Key, kvp.Value.ToJson());
                }

                byte[] contents = Encoding.UTF8.GetBytes(json.ToString());

                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;

                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(contents, 0, contents.Length);
                    stream.Close();
                }

                response.Close();
            }
            catch (Exception e)
            {
                byte[] contents = Encoding.UTF8.GetBytes(e.ToString());

                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;

                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(contents, 0, contents.Length);
                    stream.Close();
                }
            }
        }

        private async void OnGetSystemInfoRequestHandlerAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                MstJson json = new MstJson();

                foreach (var kvp in dashboardModule.SystemInfo)
                {
                    json.AddField(kvp.Key, kvp.Value.ToJson());
                }

                byte[] contents = Encoding.UTF8.GetBytes(json.ToString());

                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;

                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(contents, 0, contents.Length);
                    stream.Close();
                }

            }
            catch (Exception e)
            {
                byte[] contents = Encoding.UTF8.GetBytes(e.ToString());

                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;

                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(contents, 0, contents.Length);
                    stream.Close();
                }
            }
        }

        private async void OnGetModulesInfoRequestHandlerAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                MstJson json = new MstJson();

                foreach (var kvp in dashboardModule.ModulesInfo)
                {
                    MstJson modules = new MstJson();

                    foreach (var item in kvp.Value)
                    {
                        modules.Add(item.Data);
                    }

                    json.AddField(kvp.Key, modules);
                }

                byte[] contents = Encoding.UTF8.GetBytes(json.ToString());

                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;

                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(contents, 0, contents.Length);
                    stream.Close();
                }

            }
            catch (Exception e)
            {
                byte[] contents = Encoding.UTF8.GetBytes(e.ToString());

                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;

                using (var stream = response.OutputStream)
                {
                    await stream.WriteAsync(contents, 0, contents.Length);
                    stream.Close();
                }
            }
        }

        #endregion
    }

    [Serializable]
    public struct MstDashboardComponent
    {
        public string name;
        public TextAsset htmlTemplate;
        public TextAsset vueScript;
    }
}