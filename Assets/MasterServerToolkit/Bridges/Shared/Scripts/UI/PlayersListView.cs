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
        [Tooltip("Required label prefab instantiated for each player index and player name cell.")]
        protected UILable uiLablePrefab;
        [SerializeField]
        [Tooltip("Required label prefab instantiated for the player-list column headers.")]
        protected UILable uiColLablePrefab;
        [SerializeField]
        [Tooltip("Container that receives generated player-list labels. Missing assignment prevents the list from being drawn and produces an MST error log.")]
        protected RectTransform listContainer;

        #endregion

        private int roomId = -1;

        protected virtual void Start()
        {
            if (listContainer)
            {
                foreach (Transform t in listContainer)
                {
                    Destroy(t.gameObject);
                }
            }
        }

        protected override void OnStartShow()
        {
            base.OnStartShow();
            roomId = Payload.AsInt();
        }

        protected override void OnEndShow()
        {
            base.OnEndShow();
            FindPlayers();
        }

        protected virtual void DrawPlayersList(GameInfoPacket game)
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

        protected virtual void ClearPlayersList()
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
        /// Sends request to master server to find games list
        /// </summary>
        public void FindPlayers()
        {
            ClearPlayersList();
            CanvasGroup.interactable = false;

            // if we have room access
            if (roomId < 0 && Mst.Client.Rooms.HasAccess)
            {
                roomId = Mst.Client.Rooms.ReceivedAccess.Id;
            }

            var filter = new MstProperties();
            filter.Set(MstParamKeys.ROOM_ID, roomId);

            Mst.Client.Matchmaker.FindGames(filter, (games) =>
            {
                CanvasGroup.interactable = true;

                if (games.Count > 0)
                {
                    GameInfoPacket game = games.First();
                    DrawPlayersList(game);
                }
            });
        }

        public void Disconnect()
        {
            Hide();
            Mst.Events.Invoke(MstEventKeys.leaveRoom);
        }
    }
}
