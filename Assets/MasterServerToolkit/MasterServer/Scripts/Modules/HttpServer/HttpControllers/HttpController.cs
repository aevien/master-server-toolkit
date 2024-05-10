using MasterServerToolkit.Logging;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class HttpController : MonoBehaviour, IHttpController
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
        public HttpServerModule HttpServer { get; set; }
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
        /// <param name="httpServer"></param>
        public virtual void Initialize(HttpServerModule httpServer)
        {
            HttpServer = httpServer;
            MasterServer = httpServer.Server;

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }
    }
}
