using MasterServerToolkit.Extensions;
using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class WebController : MonoBehaviour, IWebController
    {
        #region INSPECTOR

        [Header("Controller Settings"), SerializeField, Tooltip("Minimum severity written by this controller while processing or rendering HTTP requests.")]
        protected LogLevel logLevel = LogLevel.Info;
        [SerializeField, Tooltip("Requires HTTP Basic Auth for this controller's routes even when server-wide credentials are disabled. Server-wide credential enforcement still protects all routes when enabled.")]
        protected bool useCredentials = false;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public bool UseCredentials
        {
            get => useCredentials;
            set => useCredentials = value;
        }

        /// <summary>
        /// Logger connected to this module
        /// </summary>
        protected Logging.Logger logger;
        /// <summary>
        /// 
        /// </summary>
        public HttpServerModule WebServer { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public ServerBehaviour Server { get; set; }

        protected virtual void OnValidate() { }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Dispose() { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="webServer"></param>
        public virtual void Initialize(HttpServerModule webServer)
        {
            WebServer = webServer;
            Server = webServer.Server;

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual MstJson JsonInfo()
        {
            MstJson json = MstJson.CreateObject();

            try
            {
                json.AddField("name", GetType().Name);
                json.AddField("description", GetType().Name);
            }
            catch (Exception e)
            {
                json.AddField("error", e.ToString());
            }

            return json;
        }
    }
}
