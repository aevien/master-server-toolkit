using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WebSocketSharp.Server;

namespace MasterServerToolkit.MasterServer
{
    public abstract class HttpController : MonoBehaviour, IHttpController
    {
        #region INSPECTOR

        [Header("Controller Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;
        [SerializeField]
        private TextAsset templateHtml;
        [SerializeField]
        private TextAsset[] cssFiles;
        [SerializeField]
        private TextAsset[] javascriptFiles;

        #endregion

        /// <summary>
        /// Html composed with this controller from css, js, template and views files
        /// </summary>
        private string composedHtml;

        protected const string MST_NAME = "#MST_NAME#";
        protected const string MST_VERSION = "#MST_VERSION#";
        protected const string CSS_HERE = "#CSS_HERE#";
        protected const string JS_HERE = "#JS_HERE#";

        /// <summary>
        /// Logger connected to this module
        /// </summary>
        protected Logging.Logger logger;

        /// <summary>
        /// 
        /// </summary>
        public HttpServerModule Server { get; set; }

        public virtual void Initialize(HttpServerModule server)
        {
            Server = server;

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            ComposeHtml();
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void ComposeHtml()
        {
            if (!templateHtml)
            {
                logger.Debug("Html template is not defined");
                composedHtml = $"Controller name: {GetType().Name}";
            }
            else
            {
                composedHtml = templateHtml.text;

                if (cssFiles != null)
                {
                    StringBuilder cssBuilder = new StringBuilder();

                    foreach (TextAsset cssFile in cssFiles)
                    {
                        cssBuilder.Append($"<!-- Start - {cssFile.name} -->");
                        cssBuilder.Append("<style>");
                        cssBuilder.Append(cssFile.text);
                        cssBuilder.Append("</style>");
                        cssBuilder.Append($"<!-- End - {cssFile.name} -->");
                    }

                    ReplaceTokenWith(CSS_HERE, cssBuilder.ToString());
                }

                if (javascriptFiles != null)
                {
                    StringBuilder javascriptBuilder = new StringBuilder();

                    foreach (TextAsset javascriptFile in javascriptFiles)
                    {
                        javascriptBuilder.Append($"<!-- Start - {javascriptFile.name} -->");
                        javascriptBuilder.Append("<script type=\"text/javascript\">");
                        javascriptBuilder.Append(javascriptFile.text);
                        javascriptBuilder.Append("</script>");
                        javascriptBuilder.Append($"<!-- End - {javascriptFile.name} -->");
                    }

                    ReplaceTokenWith(JS_HERE, javascriptBuilder.ToString());
                }

                ReplaceTokenWith(MST_VERSION, Mst.Version);
                ReplaceTokenWith(MST_NAME, Mst.Name);
            }
        }

        /// <summary>
        /// Replace given token with given value
        /// </summary>
        /// <param name="token"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public string ReplaceTokenWith(string token, string value)
        {
            composedHtml = composedHtml.Replace(token, value);
            return composedHtml;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] GetResponseBytes()
        {
            return Encoding.UTF8.GetBytes(composedHtml);
        }
    }
}
