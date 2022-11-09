using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public class DynamicSingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
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

        protected static bool _wasCreated = false;
        protected static T _instance;

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(typeof(T).Name);
            logger.LogLevel = logLevel;

            if (_instance != null)
            {
                isNowDestroying = true;
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _wasCreated = false;
                _instance = null;
            }
        }

        protected static bool TryGetOrCreate(out T instance)
        {
            if (_instance == null && !_wasCreated)
            {
                var instanceObj = new GameObject();
                _instance = instanceObj.AddComponent<T>();
                instanceObj.name = $"--{_instance.GetType().Name}".ToUpper();
                _wasCreated = true;

                DontDestroyOnLoad(_instance);
            }

            instance = _instance;
            return _instance != null;
        }
    }
}