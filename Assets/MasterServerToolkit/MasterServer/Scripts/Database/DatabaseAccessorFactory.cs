using MasterServerToolkit.Logging;
using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class DatabaseAccessorFactory : MonoBehaviour
    {
        #region INSPECOTOR

        [SerializeField]
        private HelpBox _header = new HelpBox()
        {
            Text = "This script is a factory, which sets up database accessors for the game"
        };

        [Header("Base Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        #endregion

        /// <summary>
        /// Logger connected to this module
        /// </summary>
        protected Logging.Logger logger;

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }

        /// <summary>
        /// Creates database or web/rest api accessor to communicate with them.
        /// </summary>
        public abstract void CreateAccessors();
    }
}