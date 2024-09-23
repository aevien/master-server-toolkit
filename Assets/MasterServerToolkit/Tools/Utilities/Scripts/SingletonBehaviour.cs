using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
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
        [SerializeField]
        protected bool isGlobal = true;

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

        /// <summary>
        /// Property to get the singleton instance.
        /// Automatically creates the instance if it doesn't exist.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (!TryGetOrCreate(out _instance))
                {
                    Debug.LogError($"Failed to create or find instance of {typeof(T)}");
                }

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(typeof(T).Name);
            logger.LogLevel = logLevel;

            if (_instance != null && _instance != this)
            {
                isNowDestroying = true;
                Destroy(gameObject);
            }
            else if (_instance == null)
            {
                _instance = this as T;
                _wasCreated = true;

                if (isGlobal)
                {
                    DontDestroyOnLoad(_instance);
                }
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

        /// <summary>
        /// Attempts to get or create the singleton instance with a specified isGlobal value.
        /// </summary>
        /// <param name="instance">Returned instance</param>
        /// <param name="isGlobal">If true, the instance will be preserved between scenes</param>
        /// <returns>True if the instance was successfully obtained or created, otherwise False</returns>
        protected static bool TryGetOrCreate(out T instance, bool isGlobal = true)
        {
            if (_instance == null && !_wasCreated)
            {
                var instanceObj = new GameObject();
                _instance = instanceObj.AddComponent<T>();
                instanceObj.name = $"--{_instance.GetType().Name}".ToUpper();
                _wasCreated = true;

                // Cast to allow setting isGlobal
                if (_instance is SingletonBehaviour<T> singleton)
                {
                    singleton.isGlobal = isGlobal;
                }
            }

            instance = _instance;
            return _instance != null;
        }
    }
}
