using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class DelayedDestroyBehaviour : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        [Tooltip("Delay in scaled seconds before this GameObject is destroyed after Start. 0 destroys it at the end of the current frame.")]
        private float delayTime = 0f;

        #endregion

        void Start()
        {
            Destroy(gameObject, delayTime);
        }
    }
}
