using MasterServerToolkit.Bridges;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class GameBridgeDemo : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Required TextMeshPro label that receives platform, player, storage, and demo-operation output. Existing text is preserved and new lines are appended.")]
        private TextMeshProUGUI output;
        [SerializeField]
        [Tooltip("Required authentication button root. The demo shows it only while the active platform player is a guest and updates its active state every frame.")]
        private GameObject authButton;

        private void Start()
        {
            Output($"{GameBridge.Service.Id} platform has been detected");

            if (!GameBridge.Service.IsReady)
            {
                GameBridge.Service.OnReadyEvent += Service_OnReadyEvent;
            }
            else
            {
                Service_OnReadyEvent(true);
            }
        }

        private void OnDestroy()
        {
            GameBridge.Service.OnReadyEvent -= Service_OnReadyEvent;
            GameBridge.Service.Player.OnAuthenticateEvent -= Service_OnPlayerEvent;
        }

        private void Update()
        {
            authButton
                .gameObject
                .SetActive(
                    GameBridge.Service != null &&
                    GameBridge.Service.Player.IsGuest &&
                    GameBridge.Service.Player.IsAuthenticationSupported);
        }

        private void Service_OnReadyEvent(bool isReady)
        {
            if (!isReady)
            {
                Output($"Error starting service {GameBridge.Service.Id}");
            }
            else
            {
                Output("Ready!");
                Output($"App: {GameBridge.Service.AppId}");
                Output($"Lang: {GameBridge.Service.Lang}");
                Output($"Device: {GameBridge.Service.Device}");
                Output($"IsMobile: {GameBridge.Service.Device == ServiceDeviceType.Mobile}");
                Output($"Payload: {GameBridge.Service.Payload.Print(true)}");
                Output($"Referrer: {GameBridge.Service.Referrer.ToJson().Print(true)}");

                GameBridge.Service.Player.OnAuthenticateEvent += Service_OnPlayerEvent;
            }
        }

        private void Service_OnPlayerEvent(IPlayerModule player)
        {
            ShowPlayerInfo(player);
        }

        private void ShowPlayerInfo(IPlayerModule player)
        {
            Output($"Player Id: {player.Id}");
            Output($"Player Avatar: {player.Avatar}");
            Output($"Player Name: {player.Name}");
            Output($"Player IsGuest: {player.IsGuest}");
        }

        private void Output(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                output.text += "-" + text;
            }
            else
            {
                output.text += "-" + text + "\n";
            }
        }

        public void OnClickAuthPlayer()
        {
            Debug.Log("Start player auth process...");

            ViewsManager.Show<LoadingInfoView>("Player auth in progress... Please wait!");

            GameBridge.Service.Player.Authenticate((isSuccess, error) =>
            {
                ViewsManager.Hide<LoadingInfoView>();

                if (!isSuccess)
                {
                    ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(error));
                }
                else
                {
                    Debug.Log("Player authenticated");
                    Output("Player authenticated");
                }
            });
        }

        public void OnClickGetPlayerData()
        {
            GameBridge.Service.Storage.LoadData((data) =>
            {
                Output($"Player Data Get: {data.Print(true)}");
            });
        }

        public void OnClickSetPlayerData()
        {
            GameBridge.Service.Storage.SetString("currentDateTime", DateTime.UtcNow.ToString());
            Output($"Player Data Set: {GameBridge.Service.Storage.Data}");
        }

        public void OnClickShowFullScreenVideo()
        {
            GameBridge.Service.Ad.ShowFullScreenVideo((status) =>
            {
                Debug.Log($"Full screen video status: {status}");
            });
        }

        public void OnClickShowRewardedVideo()
        {
            GameBridge.Service.Ad.ShowRewardedVideo((status) =>
            {
                Debug.Log($"Rewarded video status: {status}");
            });
        }

        public void OnClickMakePurchase()
        {
            GameBridge.Service.IAP.Purchase("coins_small", (purchaseInfo) =>
            {
                Debug.Log($"Purchase result: {purchaseInfo}");
            });
        }

        public void OnClickGetProducts()
        {
            GameBridge.Service.IAP.GetProducts((products) =>
            {
                foreach (var product in products)
                {
                    Debug.Log(product);
                }
            });
        }

        public void OnClickGetPurchases()
        {
            GameBridge.Service.IAP.GetPurchases(null);
        }
    }
}
