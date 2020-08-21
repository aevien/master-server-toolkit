using MasterServerToolkit.MasterServer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aevien.Utilities
{
    public class DynamicSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// Singleton state
        /// </summary>
        private static bool _initialized;

        /// <summary>
        /// Id of the main thread Unity's API runs in
        /// </summary>
        static int _mainThreadId = -1;

        private static T _instance { get; set; }

        public static T Instance
        {
            get
            {
                if (!_initialized)
                    Create();

                return _instance;
            }
        }

        public static void Create()
        {
            Create(string.Empty);
        }

        public static void Create(string name)
        {
            if (Mst.Runtime.SupportsThreads && _initialized && _mainThreadId != -1 && _mainThreadId == Mst.Concurrency.CurrentThreadId)
                return;

            if (!_initialized)
            {
                string newName = !string.IsNullOrEmpty(name) ? name : $"--{typeof(T).Name}".ToUpper();
                var go = new GameObject(newName);
                _instance = go.AddComponent<T>();
                DontDestroyOnLoad(_instance);
                _initialized = true;

                if (Mst.Runtime.SupportsThreads)
                {
                    _mainThreadId = Mst.Concurrency.CurrentThreadId;
                }
            }
        }

        private void OnDestroy()
        {
            _initialized = false;
        }
    }
}
