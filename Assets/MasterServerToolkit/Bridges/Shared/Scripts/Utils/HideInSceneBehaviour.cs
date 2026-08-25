using UnityEngine;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.Bridges
{
    public class HideInSceneBehaviour : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        [Tooltip("Exact scene name in which this GameObject is disabled. In every other scene the object remains active.")]
        private string sceneName;

        #endregion

        void Start()
        {
            gameObject.SetActive(SceneManager.GetActiveScene().name != sceneName);
        }
    }
}
