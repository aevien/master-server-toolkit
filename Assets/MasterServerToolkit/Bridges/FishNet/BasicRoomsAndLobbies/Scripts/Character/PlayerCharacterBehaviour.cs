#if FISHNET
using FishNet.Object;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.Bridges.FishNetworking.Character
{
    public class PlayerCharacterBehaviour : NetworkBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Base Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        #endregion

        /// <summary>
        /// Logger assigned to this module
        /// </summary>
        protected Logging.Logger logger;

        /// <summary>
        /// Check if this behaviour is ready
        /// </summary>
        public virtual bool IsReady { get; protected set; } = true;

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }
    }
}
#endif