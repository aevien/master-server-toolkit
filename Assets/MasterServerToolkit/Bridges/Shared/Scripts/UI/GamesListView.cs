using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class GamesListView : UIView
    {
        [Header("Components"), SerializeField]
        [Tooltip("Required label prefab instantiated for each room data cell in the generated list.")]
        private UILable uiLablePrefab;
        [SerializeField]
        [Tooltip("Required label prefab instantiated for the generated table column headers.")]
        private UILable uiColLablePrefab;
        [SerializeField]
        [Tooltip("Required button prefab instantiated for player-list and room-join actions in each generated row.")]
        private Button buttonPrefab;
        [SerializeField]
        [Tooltip("Required RectTransform that receives all generated headers, room cells, and action buttons. Existing children are removed when the list is cleared.")]
        private RectTransform listContainer;
        [SerializeField]
        [Tooltip("Required status label used while rooms are being requested and when no rooms are found outside the Editor.")]
        private TMP_Text statusInfoText;

        [Tooltip("Optional Inspector event reserved for view integrations. This component does not invoke it internally.")]
        public UnityEvent OnStartGameEvent;

        protected void Start()
        {
            if (listContainer)
            {
                foreach (Transform t in listContainer)
                {
                    Destroy(t.gameObject);
                }
            }
        }

        protected override void OnEndShow()
        {
            base.OnEndShow();
            FindGames();
        }

        private void DrawGamesList(IEnumerable<GameInfoPacket> games)
        {
            List<GameInfoPacket> gameList = new List<GameInfoPacket>(games);

            var gameNumberCol = Instantiate(uiColLablePrefab, listContainer, false);
            gameNumberCol.Text = "#";
            gameNumberCol.name = "gameNumberCol";

            var gameNameCol = Instantiate(uiColLablePrefab, listContainer, false);
            gameNameCol.Text = "Name";
            gameNameCol.name = "gameNameCol";

            var gameAddressCol = Instantiate(uiColLablePrefab, listContainer, false);
            gameAddressCol.Text = "Address";
            gameAddressCol.name = "gameAddressCol";

            var gameRegionCol = Instantiate(uiColLablePrefab, listContainer, false);
            gameRegionCol.Text = "Region";
            gameRegionCol.name = "gameRegionCol";

            var pingRegionCol = Instantiate(uiColLablePrefab, listContainer, false);
            pingRegionCol.Text = "Ping";
            pingRegionCol.name = "pingRegionCol";

            var gamePlayersCol = Instantiate(uiColLablePrefab, listContainer, false);
            gamePlayersCol.Text = "Players";
            gamePlayersCol.name = "gamePlayersCol";

            var connectBtnCol = Instantiate(uiColLablePrefab, listContainer, false);
            connectBtnCol.Text = "Action";
            connectBtnCol.name = "connectBtnCol";

            for (int i = 0; i < gameList.Count; i++)
            {
                GameInfoPacket gameInfo = gameList[i];
                var gameNumberLable = Instantiate(uiLablePrefab, listContainer, false);
                gameNumberLable.Text = $"{i + 1}";
                gameNumberLable.name = $"gameNumberLable_{i}";

                var gameNameLable = Instantiate(uiLablePrefab, listContainer, false);
                gameNameLable.Text = gameInfo.IsPasswordProtected ? $"{gameInfo.Name} <color=yellow>[Password]</color>" : gameInfo.Name;
                gameNameLable.name = $"gameNameLable_{i}";

                var gameAddressLable = Instantiate(uiLablePrefab, listContainer, false);
                gameAddressLable.Text = gameInfo.Address;
                gameAddressLable.name = $"gameAddressLable_{i}";

                var gameRegionLable = Instantiate(uiLablePrefab, listContainer, false);
                string region = string.IsNullOrEmpty(gameInfo.Region) ? "International" : gameInfo.Region;
                gameRegionLable.Text = region;
                gameRegionLable.name = $"gameRegionLable_{i}";

                var pingRegionLable = Instantiate(uiLablePrefab, listContainer, false);
                pingRegionLable.Text = $"...";

                var rx = new Regex(@":\d+");
                string ip = rx.Replace(gameInfo.Address.Trim(), "");

                MstTimer.WaitPing(ip, (time) =>
                {
                    pingRegionLable.Text = $"{time} ms.";
                });

                pingRegionLable.name = $"pingRegionLable_{i}";

                var gamePlayersBtn = Instantiate(buttonPrefab, listContainer, false);
                gamePlayersBtn.name = $"gamePlayersLable_{i}";

                string maxPleyers = gameInfo.MaxPlayers <= 0 ? "∞" : gameInfo.MaxPlayers.ToString();
                gamePlayersBtn.GetComponentInChildren<TextMeshProUGUI>().text = $"{gameInfo.OnlinePlayers} / {maxPleyers} [Show]";
                gamePlayersBtn.onClick.AddListener(() =>
                {
                    ViewsManager.Show<SignUpView>(gameInfo.Id);
                    Hide();
                });

                var gameConnectBtn = Instantiate(buttonPrefab, listContainer, false);
                gameConnectBtn.name = $"gameConnectBtn_{i}";
                gameConnectBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Join";
                gameConnectBtn.onClick.AddListener(() =>
                {
                    MatchmakingBehaviour.Instance.StartMatch(gameInfo);
                });

                logger.Info(gameInfo);
            }
        }

        private void ClearGamesList()
        {
            if (listContainer)
            {
                foreach (Transform tr in listContainer)
                {
                    Destroy(tr.gameObject);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ShowCreateNewRoomView()
        {
            Mst.Events.Invoke(MstEventKeys.showCreateNewRoomView);
        }

        /// <summary>
        /// Sends request to master server to find games list
        /// </summary>
        public void FindGames()
        {
            ClearGamesList();

            CanvasGroup.interactable = false;

            statusInfoText.text = "Finding rooms... Please wait!";

            Mst.Client.Matchmaker.FindGames((games) =>
            {
                CanvasGroup.interactable = true;
#if !UNITY_EDITOR
                    if (games.Count == 0)
                    {
                        statusInfoText.text = "No games found! Try to create your own one.";
                        return;
                    }
#endif
                statusInfoText.text = "";

                DrawGamesList(games);
            });
        }
    }
}
