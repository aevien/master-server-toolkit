#if MIRROR
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Bridges.Mirror.Character
{
    public class PlayerCharacterBehaviour : NetworkBehaviour
    {
        /// <summary>
        /// Logger assigned to this module
        /// </summary>
        protected Logging.Logger logger;

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Base Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

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