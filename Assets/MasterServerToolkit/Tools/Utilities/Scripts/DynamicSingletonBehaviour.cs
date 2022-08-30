using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System.Collections;
using UnityEditor;
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
        protected static bool isNowDestroying = false;
        /// <summary>
        /// Current instance of this singleton
        /// </summary>
        protected static T _instance;
        /// <summary>
        /// 
        /// </summary>
        protected static bool isQuitting = false;

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(typeof(T).Name);
            logger.LogLevel = logLevel;

            StartCoroutine(WaitAndDestroy());
        }

        private void OnDestroy()
        {
            isNowDestroying = true;
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        /// <summary>
        /// Current instance of this singleton
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null && !isQuitting && !isNowDestroying)
                    Create();

                return _instance;
            }
        }

        private IEnumerator WaitAndDestroy()
        {
            yield return new WaitForEndOfFrame();

            if (_instance != null && _instance != this)
                Destroy(gameObject);
        }

        /// <summary>
        /// Tries to create new instance of this singleton
        /// </summary>
        public static void Create()
        {
            Create(string.Empty);
        }

        /// <summary>
        /// Tries to create new instance of this singleton with new given name
        /// </summary>
        /// <param name="name"></param>
        public static void Create(string name)
        {
            if (_instance || isQuitting || isNowDestroying)
                return;

            string newName = !string.IsNullOrEmpty(name) ? name : $"--{typeof(T).Name}".ToUpper();
            var go = new GameObject(newName);
            _instance = go.AddComponent<T>();
            DontDestroyOnLoad(_instance);
        }
    }
}
