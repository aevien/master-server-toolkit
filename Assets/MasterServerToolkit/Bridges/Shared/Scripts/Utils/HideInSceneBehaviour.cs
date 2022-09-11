using UnityEngine;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.Games
{
    public class HideInSceneBehaviour : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private string sceneName;

        #endregion

        void Start()
        {
            gameObject.SetActive(SceneManager.GetActiveScene().name != sceneName);
        }
    }
}