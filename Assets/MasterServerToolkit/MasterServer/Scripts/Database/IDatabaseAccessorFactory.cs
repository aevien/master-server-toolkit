using MasterServerToolkit.Logging;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class DatabaseAccessorFactory : MonoBehaviour
    {
        #region INSPECOTOR

        /// <summary>
        /// Logger connected to this module
        /// </summary>
        protected Logging.Logger logger;

        [Header("Base Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        #endregion

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