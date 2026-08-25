using MasterServerToolkit.Networking;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class OpenURLInBrowser : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        [Tooltip("Absolute URL opened by Open(). Leave empty only when the component is intentionally disabled.")]
        private string url;
        [SerializeField, Tooltip("Delay in realtime seconds between calling Open() and opening the URL. 0 opens it immediately through the timer callback.")]
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
                MasterServerToolkit.Logging.Logs.Error(
                    $"Cannot open URL because field {nameof(url)} is empty",
                    MasterServerToolkit.Logging.LogChannels.System);
            }
        }
    }
}
