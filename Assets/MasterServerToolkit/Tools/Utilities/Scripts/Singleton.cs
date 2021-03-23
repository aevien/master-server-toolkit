using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        protected bool isNowDestroying = false;

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
        }
    }
}