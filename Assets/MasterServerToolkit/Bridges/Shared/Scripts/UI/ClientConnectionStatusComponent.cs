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
        private bool changeStatusColor = true;

        [Header("Status Colors"), SerializeField]
        private Color unknownStatusColor = Color.yellow;
        [SerializeField]
        private Color onlineStatusColor = Color.green;
        [SerializeField]
        private Color connectingStatusColor = Color.cyan;
        [SerializeField]
        private Color offlineStatusColor = Color.red;

        [Header("Components"), SerializeField]
        private Image statusImage;
        [SerializeField]
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
                    RepaintStatus($"{Mst.Localization["connectionStatusConnected"]}\n{address}", onlineStatusColor);
                    break;
                case ConnectionStatus.Disconnected:
                    RepaintStatus($"{Mst.Localization["connectionStatusDisconnected"]}", offlineStatusColor);
                    break;
                case ConnectionStatus.Connecting:
                    RepaintStatus($"{Mst.Localization["connectionStatusConnecting"]}\n{address}", connectingStatusColor);
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