using MasterServerToolkit.Bridges;
using MasterServerToolkit.MasterServer;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.GameService
{
    public class GameBridgeDemo : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI output;
        [SerializeField]
        private GameObject authButton;

        private void Start()
        {
            output.text = $"{GameBridge.Service.Id} platform has been detected\n";

            GameBridge.Service.OnReadyEvent += Service_OnReadyEvent;
            GameBridge.Service.OnPlayerInfoEvent += Service_OnPlayerEvent;
        }

        private void OnDestroy()
        {
            GameBridge.Service.OnReadyEvent -= Service_OnReadyEvent;
            GameBridge.Service.OnPlayerInfoEvent -= Service_OnPlayerEvent;
        }

        private void Update()
        {
            authButton.GetComponentInChildren<Button>().interactable = GameBridge.Service.Player.IsGuest;
        }

        private void Service_OnReadyEvent()
        {
            output.text += "Ready!\n";
            output.text += $"App: {GameBridge.Service.AppId}\n";
            output.text += $"Lang: {GameBridge.Service.Lang}\n";
            output.text += $"Device: {GameBridge.Service.DeviceType}\n";
            output.text += $"IsMobile: {GameBridge.Service.IsMobile}\n";
            output.text += $"Payload: {GameBridge.Service.Payload.Print(true)}\n";
        }

        private void Service_OnPlayerEvent(PlayerInfo player)
        {
            ShowPlayerInfo(player);
        }

        private void ShowPlayerInfo(PlayerInfo player)
        {
            output.text += $"Player Id: {player.Id}\n";
            output.text += $"Player Avatar: {player.Avatar}\n";
            output.text += $"Player Name: {player.Name}\n";
            output.text += $"Player IsGuest: {player.IsGuest}\n";
            output.text += $"Player Extra: {player.Extra.Print(true)}\n";
        }

        public void OnClickAuthPlayer()
        {
            Debug.Log("Start player auth process...");

            GameBridge.Service.Authenticate((isSuccess, error) =>
            {
                if (!isSuccess)
                {
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(error));
                }
                else
                {
                    Debug.Log("Player authenticated");
                    output.text += "Player authenticated\n";
                }
            });
        }

        public void OnClickGetPlayerData()
        {
            GameBridge.Service.LoadPlayerData((data) =>
            {
                output.text += $"Player Data Get: {data.Print(true)}\n";
            });
        }

        public void OnClickSetPlayerData()
        {
            GameBridge.Service.SavePlayerData("currentDateTime", DateTime.UtcNow.ToString(), (isSuccess, error) =>
            {
                output.text += $"Player Data Set: {isSuccess}\n";
            });
        }

        public void OnClickShowFullScreenVideo()
        {
            GameBridge.Service.ShowFullScreenVideo((status) =>
            {
                Debug.Log($"Full screen video status: {status}");
            });
        }

        public void OnClickShowRewardedVideo()
        {
            GameBridge.Service.ShowRewardedVideo((status) =>
            {
                Debug.Log($"Rewarded video status: {status}");
            });
        }

        public void OnClickMakePurchase()
        {
            GameBridge.Service.Purchase("coins_small", (purchaseInfo) =>
            {
                Debug.Log($"Purchase result: {purchaseInfo}");
            });
        }

        public void OnClickGetProducts()
        {
            GameBridge.Service.GetProducts((products) =>
            {
                foreach (var product in products)
                {
                    Debug.Log(product);
                }
            });
        }

        public void OnClickGetPurchases()
        {
            GameBridge.Service.GetPurchases(null);
        }
    }
}