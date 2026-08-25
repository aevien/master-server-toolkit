using MasterServerToolkit.Extensions;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class HtmlWebController : WebController
    {
        #region INSPECTOR

        [Header("Html Settings"), SerializeField, Tooltip("Relative HTTP route registered for this page, without a leading slash. Empty values are generated from the controller type name.")]
        protected string route;
        [SerializeField, Tooltip("Optional outer HTML template containing MST replacement tokens and the inner-html token. Falls back to the owning HtmlServerModule template or a minimal inner page.")]
        private TextAsset templateAsset;
        [SerializeField, Tooltip("HTML fragment inserted into the selected template when this page is requested. Leave None only when Page() supplies content from code.")]
        private TextAsset pageAsset;
        [SerializeField, Tooltip("Optional page-specific JavaScript inserted into the template JavaScript token. The shared HtmlServerModule script is inserted separately.")]
        private TextAsset javascriptAsset;
        [SerializeField, Tooltip("Optional page icon encoded as Base64. The source texture must have Read/Write enabled; otherwise the module icon is used when available.")]
        private Sprite icon;
        [SerializeField, Tooltip("Page title exposed to templates and dashboard navigation.")]
        private string title = "New Html Page";
        [SerializeField, TextArea(), Tooltip("Short page description exposed to templates or navigation metadata.")]
        private string description = "New Html Page";

        #endregion

        private string template = WebServerTokens.INNER_HTML;
        private string page = string.Empty;
        private string javascript = string.Empty;
        private string iconBase64 = string.Empty;

        public string Title => title;
        public string Description => description;
        public Sprite Icon => icon;
        public string IconBase64 => iconBase64;
        public string Template => template;
        public string Route => route;

        protected override void OnValidate()
        {
            base.OnValidate();

            if (string.IsNullOrEmpty(route))
            {
                route = GetType()
                    .Name
                    .FromCamelcase()
                    .Replace(" ", "-")
                    .ToLower();
            }

            if (string.IsNullOrEmpty(title))
            {
                title = GetType()
                    .Name
                    .FromCamelcase()
                    .Replace(" ", "-")
                    .ToLower();
            }
        }

        public override void Initialize(HttpServerModule webServer)
        {
            base.Initialize(webServer);

            if (templateAsset != null)
                template = templateAsset.text;

            if (pageAsset != null)
                page = pageAsset.text;

            if (javascriptAsset != null)
                javascript = javascriptAsset.text;

            if (Icon != null)
                iconBase64 = icon.texture.ToBase64Url();

            if (webServer is HtmlServerModule module)
            {
                if (Icon == null && module.Icon != null)
                    iconBase64 = module.Icon.texture.ToBase64Url();

                if (templateAsset == null && module.Template != null)
                    template = module.Template.text;

                if (module.Javascript != null)
                {
                    template = template.Replace(WebServerTokens.JAVA_SCRIPT, module.Javascript.text);
                }
                else
                {
                    template = template.Replace(WebServerTokens.JAVA_SCRIPT, "");
                }
            }

            webServer.RegisterGetHandler(route, PageHandler, UseCredentials);
        }

        protected virtual Task<IHttpResult> PageHandler(HttpListenerRequest request)
        {
            var result = new HtmlResult(Page());
            return Task.FromResult<IHttpResult>(result);
        }

        protected virtual string Page()
        {
            return Combine(page);
        }

        protected virtual string Combine(string html)
        {
            string result = template;

            if (!string.IsNullOrEmpty(template))
            {
                result = result.Replace(WebServerTokens.INNER_HTML, html);
                result = result.Replace(WebServerTokens.JAVA_SCRIPT, javascript);
                result = result.Replace(WebServerTokens.MST_TITLE, $"{Mst.Name} v.{Mst.Version}");
                result = result.Replace(WebServerTokens.MST_FAVICON, iconBase64);
                result = result.Replace(WebServerTokens.MST_BRAND_ICON, iconBase64);
            }
            else
            {
                result = html;
            }

            return result;
        }
    }
}
