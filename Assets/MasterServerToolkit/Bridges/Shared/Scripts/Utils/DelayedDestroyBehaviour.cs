using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    public class DelayedDestroyBehaviour : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private float delayTime = 0f;

        #endregion

        void Start()
        {
            Destroy(gameObject, delayTime);
        }
    }
}
