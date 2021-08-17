using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System.Collections;
using System.Collections.Generic;
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
        protected bool isNowDestroying = false;

        public static T Singleton { get; protected set; }

        protected virtual void Awake()
        {
            if (Singleton != null)
            {
                isNowDestroying = true;
                Destroy(gameObject);
                return;
            }

            Singleton = this as T;

            DontDestroyOnLoad(gameObject);

            logger = Mst.Create.Logger(typeof(T).Name);
            logger.LogLevel = logLevel;
        }
    }
}