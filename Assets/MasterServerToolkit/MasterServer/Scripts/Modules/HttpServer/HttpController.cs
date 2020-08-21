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
        /// <summary>
        /// Html composed with this controller from css, js, template and views files
        /// </summary>
        private string composedHtml;

        /// <summary>
        /// Logger connected to this module
        /// </summary>
        protected MasterServerToolkit.Logging.Logger logger;

        [Header("Controller Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        [SerializeField]
        private TextAsset templateHtml;
        [SerializeField]
        private TextAsset[] cssFiles;
        [SerializeField]
        private TextAsset[] javascriptFiles;

        public virtual void Initialize(HttpServerModule server)
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            ComposeHtml();
        }

        protected virtual void ComposeHtml()
        {
            if (!templateHtml)
            {
                logger.Error("Html template is not defined");
                return;
            }

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

                ReplaceTokenWith("#CSS_HERE#", cssBuilder.ToString());
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

                ReplaceTokenWith("#JS_HERE#", javascriptBuilder.ToString());
            }

            ReplaceTokenWith("#MSF_VERSION#", Mst.Version);
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

        public byte[] GetHtmlBytes()
        {
            return Encoding.UTF8.GetBytes(composedHtml);
        }
    }
}
