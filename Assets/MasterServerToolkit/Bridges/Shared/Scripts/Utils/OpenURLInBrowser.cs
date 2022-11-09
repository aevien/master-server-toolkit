using MasterServerToolkit.Networking;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class OpenURLInBrowser : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private string url;
        [SerializeField]
        private float delayTime = 0f;

        #endregion

        public void Open()
        {
            if (!string.IsNullOrEmpty(url))
            {
                MstTimer.WaitForRealtimeSeconds(delayTime, () =>
                {
                    Application.OpenURL(url);
                });
            }
            else
            {
                Debug.LogError($"Field {nameof(url)} is empty");
            }
        }
    }
}