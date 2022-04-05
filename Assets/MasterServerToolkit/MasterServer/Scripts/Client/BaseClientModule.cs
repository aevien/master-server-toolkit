using MasterServerToolkit.Logging;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class BaseClientModule : MonoBehaviour, IBaseClientModule
    {
        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Base Module Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        /// <summary>
        /// Logger assigned to this module
        /// </summary>
        protected Logging.Logger logger;

        public BaseClientBehaviour ParentBehaviour { get; set; }

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }

        public virtual void OnInitialize(BaseClientBehaviour parentBehaviour) { }

        protected virtual void OnDestroy() { }
    }
}
