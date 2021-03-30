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

        #endregion

        /// <summary>
        /// Logger connected to this module
        /// </summary>
        protected Logging.Logger logger;
        /// <summary>
        /// 
        /// </summary>
        public HttpServerModule HttpServer { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public ServerBehaviour MasterServer { get; set; }

        public virtual void Initialize(HttpServerModule httpServer)
        {
            HttpServer = httpServer; 
            MasterServer = httpServer.Server;

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }
    }
}
