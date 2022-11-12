using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class PlayersListView : UIView
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private UILable uiLablePrefab;
        [SerializeField]
        private UILable uiColLablePrefab;
        [SerializeField]
        private RectTransform listContainer;

        #endregion

        private int roomId = -1;

        protected override void Awake()
        {
            base.Awake();

            // Listen to show/hide events
            Mst.Events.AddListener(MstEventKeys.showPlayersListView, OnShowPlayersListEventHandler);
            Mst.Events.AddListener(MstEventKeys.hidePlayersListView, OnHidePlayersListEventHandler);
        }

        protected override void Start()
        {
            base.Start();

            if (listContainer)
            {
                foreach (Transform t in listContainer)
                {
                    Destroy(t.gameObject);
                }
            }
        }

        private void OnShowPlayersListEventHandler(EventMessage message)
        {
            roomId = message.AsInt();
            Show();
        }

        private void OnHidePlayersListEventHandler(EventMessage message)
        {
            roomId = -1;
            Hide();
        }

        protected override void OnShow()
        {
            FindPlayers();
        }

        /// <summary>
        /// Sends request to master server to find games list
        /// </summary>
        public void FindPlayers()
        {
            ClearPlayersList();
            canvasGroup.interactable = false;

            // if we have room access
            if (roomId < 0 && Mst.Client.Rooms.HasAccess)
            {
                roomId = Mst.Client.Rooms.ReceivedAccess.RoomId;
            }

            var filter = new MstProperties();
            filter.Set(MstDictKeys.ROOM_ID, roomId);

            Mst.Client.Matchmaker.FindGames(filter, (games) =>
            {
                canvasGroup.interactable = true;

                if (games.Count > 0)
                {
                    GameInfoPacket game = games.First();
                    DrawPlayersList(game);
                }
            });
        }

        private void DrawPlayersList(GameInfoPacket game)
        {
            if (listContainer)
            {
                int index = 0;

                var playerIndoexCol = Instantiate(uiColLablePrefab, listContainer, false);
                playerIndoexCol.Text = "#";

                var playerNameCol = Instantiate(uiColLablePrefab, listContainer, false);
                playerNameCol.Text = "Name";

                foreach (string player in game.OnlinePlayersList)
                {
                    bool isMasterUser = game.Properties.AsString("-room.masterUser") == player;

                    var playerIndoexLable = Instantiate(uiLablePrefab, listContainer, false);
                    playerIndoexLable.Text = (index + 1).ToString();
                    playerIndoexLable.name = $"playerIndoexLable_{index}";

                    var playerNameLable = Instantiate(uiLablePrefab, listContainer, false);
                    playerNameLable.Text = $"{player} {(isMasterUser ? ":)" : "")}";
                    playerNameLable.name = $"playerNameLable_{index}";

                    index++;
                }
            }
            else
            {
                logger.Error("Not all components are setup");
            }
        }

        private void ClearPlayersList()
        {
            if (listContainer)
            {
                foreach (Transform tr in listContainer)
                {
                    Destroy(tr.gameObject);
                }
            }
        }
    }
}