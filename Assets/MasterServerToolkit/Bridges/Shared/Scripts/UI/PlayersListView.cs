using Aevien.UI;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Games
{
    public class PlayersListView : UIView
    {
        [Header("Components"), SerializeField]
        private UILable uiLablePrefab;
        [SerializeField]
        private UILable uiColLablePrefab;
        [SerializeField]
        private RectTransform listContainer;

        protected override void Awake()
        {
            base.Awake();

            // Listen to show/hide events
            Mst.Events.AddEventListener(MstEventKeys.showPlayersListView, OnShowPlayersListEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hidePlayersListView, OnHidePlayersListEventHandler);
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
            Show();
        }

        private void OnHidePlayersListEventHandler(EventMessage message)
        {
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

            Mst.Client.Matchmaker.GetPlayers((players) =>
            {
                DrawPlayersList(players);
                canvasGroup.interactable = true;
            });
        }

        private void DrawPlayersList(List<string> players)
        {
            if (listContainer)
            {
                int index = 0;

                var playerIndoexCol = Instantiate(uiColLablePrefab, listContainer, false);
                playerIndoexCol.Lable = "#";

                var playerNameCol = Instantiate(uiColLablePrefab, listContainer, false);
                playerNameCol.Lable = "Name";

                foreach (string player in players)
                {
                    var playerIndoexLable = Instantiate(uiLablePrefab, listContainer, false);
                    playerIndoexLable.Lable = (index + 1).ToString();
                    playerIndoexLable.name = $"playerIndoexLable_{index}";

                    var playerNameLable = Instantiate(uiLablePrefab, listContainer, false);
                    playerNameLable.Lable = player;
                    playerNameLable.name = $"playerNameLable_{index}";

                    index++;
                }
            }
            else
            {
                Logs.Error("Not all components are setup");
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