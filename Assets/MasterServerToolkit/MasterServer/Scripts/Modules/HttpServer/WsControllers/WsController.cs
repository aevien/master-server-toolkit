using MasterServerToolkit.Logging;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class WsController : MonoBehaviour, IWsController
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
        /// <summary>
        /// 
        /// </summary>
        public WsControllerService WsService { get; set; }

        public virtual void Initialize(HttpServerModule httpServer, WsControllerService wsService)
        {
            HttpServer = httpServer;
            MasterServer = httpServer.Server;

            WsService = wsService;

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }
    }
}
