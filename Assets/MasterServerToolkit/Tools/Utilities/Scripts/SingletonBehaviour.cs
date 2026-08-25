using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    internal static class SingletonRuntimeState
    {
        public static bool IsQuitting { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsQuitting = false;
        }
    }

    public class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Log level of this connector
        /// </summary>
        [Header("Base Settings"), SerializeField]
        [Tooltip("Minimum severity emitted by this singleton's MST logger. Choose a higher level to reduce runtime log output.")]
        protected LogLevel logLevel = LogLevel.Info;
        [SerializeField]
        [Tooltip("Keeps the singleton GameObject alive across scene loads with DontDestroyOnLoad. Disable for a scene-owned instance that should be destroyed with its scene.")]
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
        protected static bool _creationHasPendingConfig = false;
        protected static bool _creationIsGlobal = true;

        /// <summary>
        /// Property to get the singleton instance.
        /// Automatically creates the instance if it doesn't exist.
        /// Uses class default behavior for global mode.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (!Application.isPlaying)
                    return null;

                if (SingletonRuntimeState.IsQuitting)
                    return _instance;

                if (!TryGetOrCreate(out _instance))
                {
                    Logs.Error($"Failed to create or find instance of {typeof(T)}");
                }

                return _instance;
            }
        }

        /// <summary>
        /// Forces singleton to use global mode (DontDestroyOnLoad).
        /// Creates singleton if it does not exist yet.
        /// </summary>
        public static void SetGlobal()
        {
            if (!Application.isPlaying)
                return;

            if (!TryGetOrCreate(out _instance, true))
            {
                Logs.Error($"Failed to create or find global instance of {typeof(T)}");
                return;
            }

            if (_instance is SingletonBehaviour<T> singleton)
            {
                singleton.isGlobal = true;
                DontDestroyOnLoad(_instance);
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

                // If this instance was created via TryGetOrCreate, apply requested persistence mode.
                if (_creationHasPendingConfig)
                {
                    isGlobal = _creationIsGlobal;
                    _creationHasPendingConfig = false;
                }

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

        protected virtual void OnApplicationQuit()
        {
            SingletonRuntimeState.IsQuitting = true;
        }

        /// <summary>
        /// Attempts to get or create the singleton instance with a specified isGlobal value.
        /// </summary>
        /// <param name="instance">Returned instance</param>
        /// <param name="isGlobal">If true, the instance will be preserved between scenes</param>
        /// <returns>True if the instance was successfully obtained or created, otherwise False</returns>
        protected static bool TryGetOrCreate(out T instance, bool isGlobal = true)
        {
            if (SingletonRuntimeState.IsQuitting)
            {
                instance = _instance;
                return instance != null;
            }

            if (_instance == null && !_wasCreated)
            {
                // Awake is called during AddComponent, so persistence mode must be requested before creation.
                _creationHasPendingConfig = true;
                _creationIsGlobal = isGlobal;

                var instanceObj = new GameObject();
                _instance = instanceObj.AddComponent<T>();
                instanceObj.name = $"--{_instance.GetType().Name}".ToUpper();
                _wasCreated = true;
            }

            instance = _instance;
            return _instance != null;
        }

        /// <summary>
        /// Gets the current live singleton instance without creating a new GameObject.
        /// </summary>
        /// <param name="instance">The current singleton instance, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when a live instance already exists.</returns>
        protected static bool TryGetExisting(out T instance)
        {
            instance = _instance;

            if (instance != null)
                return true;

            instance = null;
            return false;
        }
    }
}
