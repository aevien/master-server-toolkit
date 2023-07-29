using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Log level of this connector
        /// </summary>
        [Header("Base Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;
        [SerializeField]
        protected bool isGlobal = false;

        #endregion

        /// <summary>
        /// Logger assigned to this module
        /// </summary>
        protected Logging.Logger logger;
        /// <summary>
        /// Check if this object is not currently being destroyed
        /// </summary>
        protected bool isNowDestroying = false;
        /// <summary>
        /// Instance of this object/>
        /// </summary>
        public static T Instance { get; protected set; }

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(typeof(T).Name);
            logger.LogLevel = logLevel;

            if (Instance != null)
            {
                isNowDestroying = true;
                Destroy(gameObject);
                return;
            }

            Instance = this as T;

            if (isGlobal)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}