using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class ClientAuthStatusComponent : MonoBehaviour
    {
        #region INSPECTOR
        [Header("Settings"), SerializeField]
        [Tooltip("When enabled, the status Image is recolored to match the current authorization state. The status text is updated regardless of this setting.")]
        private bool changeStatusColor = true;

        [Header("Status Colors"), SerializeField]
        [Tooltip("Color applied to the status Image when the client is not signed in or account data is unavailable.")]
        private Color unauthorizedStatusColor = Color.red;
        [SerializeField]
        [Tooltip("Color applied to the status Image for a signed-in non-guest account.")]
        private Color authorizedStatusColor = Color.green;
        [SerializeField]
        [Tooltip("Color applied to the status Image for a signed-in guest account.")]
        private Color guestStatusColor = Color.yellow;

        [Header("Components"), SerializeField]
        [Tooltip("Optional Image recolored for the current authorization state when Change Status Color is enabled.")]
        private Image statusImage;
        [SerializeField]
        [Tooltip("Optional text label that displays the signed-in username or the localized unauthorized status.")]
        private TextMeshProUGUI statusText;
        #endregion

        protected virtual void Update()
        {
            if (Mst.Client.Auth.IsSignedIn)
            {
                if (Mst.Client.Auth.Account != null)
                {
                    if (!Mst.Client.Auth.Account.IsGuest)
                    {
                        RepaintStatus(Mst.Client.Auth.Account.Username, authorizedStatusColor);
                    }
                    else
                    {
                        RepaintStatus(Mst.Client.Auth.Account.Username, guestStatusColor);
                    }
                }
                else
                {
                    RepaintStatus($"{Mst.Localization["ui.status.auth.unauthorized"]}", unauthorizedStatusColor);
                }
            }
            else
            {
                RepaintStatus($"{Mst.Localization["ui.status.auth.unauthorized"]}", unauthorizedStatusColor); ;
            }
        }

        private void RepaintStatus(string statusMsg, Color statusColor)
        {
            if (changeStatusColor && statusImage != null)
                statusImage.color = statusColor;

            if (statusText != null)
                statusText.text = statusMsg;
        }
    }
}
