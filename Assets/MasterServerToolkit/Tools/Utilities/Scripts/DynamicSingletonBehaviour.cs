using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public class DynamicSingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// Check if this object is not currently being destroyed
        /// </summary>
        protected bool isNowDestroying = false;
        /// <summary>
        /// Current instance of this singleton
        /// </summary>
        private static T _singleton { get; set; }

        protected virtual void Awake()
        {
            StartCoroutine(WaitAndDestroy());
        }

        private IEnumerator WaitAndDestroy()
        {
            yield return new WaitForEndOfFrame();

            if (_singleton != null && _singleton != this)
            {
                isNowDestroying = true;
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Current instance of this singleton
        /// </summary>
        public static T Singleton
        {
            get
            {
                if (!_singleton)
                    Create();

                return _singleton;
            }
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
            if (_singleton)
                return;

            _singleton = FindObjectOfType<T>();

            if (_singleton)
                return;

            string newName = !string.IsNullOrEmpty(name) ? name : $"--{typeof(T).Name}".ToUpper();
            var go = new GameObject(newName);
            _singleton = go.AddComponent<T>();
            DontDestroyOnLoad(_singleton);
        }
    }
}
