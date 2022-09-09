using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Log level of this connector
        /// </summary>
        [Header("Base Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        #endregion

        /// <summary>
        /// Logger assigned to this module
        /// </summary>
        protected Logging.Logger logger;
        /// <summary>
        /// Check if this object is not currently being destroyed
        /// </summary>
        [NonSerialized]
        protected bool isNowDestroying = false;

        /// <summary>
        /// Instance of this object/>
        /// </summary>
        public static T Instance { get; protected set; }

        protected virtual void Awake()
        {
            if (Instance != null)
            {
                isNowDestroying = true;
                Destroy(gameObject);
                return;
            }

            Instance = this as T;
            DontDestroyOnLoad(gameObject);

            logger = Mst.Create.Logger(typeof(T).Name);
            logger.LogLevel = logLevel;
        }

        protected virtual void OnDestroy()
        {
            Instance = null;
            isNowDestroying = false;
        }
    }
}