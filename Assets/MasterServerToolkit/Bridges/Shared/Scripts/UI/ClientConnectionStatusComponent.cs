using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class ClientConnectionStatusComponent : MonoBehaviour
    {
        #region INSPECTOR
        [Header("Settings"), SerializeField]
        [Tooltip("When enabled, the status Image is recolored when the MST connection state changes. The status text is updated regardless of this setting.")]
        private bool changeStatusColor = true;

        [Header("Status Colors"), SerializeField]
        [Tooltip("Color applied when the connection reports a state not handled by this component.")]
        private Color unknownStatusColor = Color.yellow;
        [SerializeField]
        [Tooltip("Color applied while the MST client is connected.")]
        private Color onlineStatusColor = Color.green;
        [SerializeField]
        [Tooltip("Color applied while the MST client is connecting or authenticating.")]
        private Color connectingStatusColor = Color.cyan;
        [SerializeField]
        [Tooltip("Color applied while the MST client is disconnected.")]
        private Color offlineStatusColor = Color.red;

        [Header("Components"), SerializeField]
        [Tooltip("Optional Image recolored for the current connection state when Change Status Color is enabled.")]
        private Image statusImage;
        [SerializeField]
        [Tooltip("Optional text label that displays the localized connection state and the remote address while connecting or connected.")]
        private TextMeshProUGUI statusText;
        #endregion

        public IClientSocket Connection => Mst.Connection;

        protected virtual void Start()
        {
            Connection.OnStatusChangedEvent += OnStatusChangedEventHandler;
            OnStatusChangedEventHandler(Connection.Status);
        }

        protected virtual void OnStatusChangedEventHandler(ConnectionStatus status)
        {
            string address = $"{Connection.Address}:{Connection.Port}";

            switch (status)
            {
                case ConnectionStatus.Connected:
                    RepaintStatus($"{Mst.Localization["ui.status.connection.connected"]}\n{address}", onlineStatusColor);
                    break;
                case ConnectionStatus.Disconnected:
                    RepaintStatus($"{Mst.Localization["ui.status.connection.disconnected"]}", offlineStatusColor);
                    break;
                case ConnectionStatus.Connecting:
                case ConnectionStatus.Authenticating:
                    RepaintStatus($"{Mst.Localization["ui.status.connection.connecting"]}\n{address}", connectingStatusColor);
                    break;
                default:
                    RepaintStatus($"Unknown status", unknownStatusColor);
                    break;
            }
        }

        private void RepaintStatus(string statusMsg, Color statusColor)
        {
            if (changeStatusColor && statusImage != null)
                statusImage.color = statusColor;

            if (statusText != null)
                statusText.text = statusMsg;
        }

        protected virtual void OnDestroy()
        {
            if (Connection != null)
                Connection.OnStatusChangedEvent -= OnStatusChangedEventHandler;
        }
    }
}
