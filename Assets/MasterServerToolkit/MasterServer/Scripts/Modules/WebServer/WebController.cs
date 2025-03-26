using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class WebController : MonoBehaviour, IWebController
    {
        #region INSPECTOR

        [Header("Controller Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;
        [SerializeField]
        private bool useCredentials = false;

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
        public WebServerModule WebServer { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public ServerBehaviour MasterServer { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public virtual void Dispose() { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="webServer"></param>
        public virtual void Initialize(WebServerModule webServer)
        {
            WebServer = webServer;
            MasterServer = webServer.Server;

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }

        public virtual MstJson JsonInfo()
        {
            MstJson json = new MstJson();

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
