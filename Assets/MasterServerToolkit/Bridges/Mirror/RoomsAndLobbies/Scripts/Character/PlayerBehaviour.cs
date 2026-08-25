#if MIRROR
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using Mirror;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    public class PlayerBehaviour : NetworkBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Base Settings"), SerializeField, Tooltip("Minimum severity written by this component's MST logger. This changes diagnostics only and does not affect network behavior.")]
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
