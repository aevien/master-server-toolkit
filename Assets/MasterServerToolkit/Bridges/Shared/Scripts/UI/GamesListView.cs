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
    public class GamesListView : UIView
    {
        [Header("Components"), SerializeField]
        private GameListItem gameItemPrefab;
        [SerializeField]
        private RectTransform listContainer;
        [SerializeField]
        private TMP_Text statusInfoText;

        public UnityEvent OnStartGameEvent;

        protected override void Awake()
        {
            base.Awake();

            // Listen to show/hide events
            Mst.Events.AddEventListener(MstEventKeys.showGamesListView, OnShowGamesListEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hideGamesListView, OnHideGamesListEventHandler);
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

        private void OnShowGamesListEventHandler(EventMessage message)
        {
            Show();
        }

        private void OnHideGamesListEventHandler(EventMessage message)
        {
            Hide();
        }

        protected override void OnShow()
        {
            FindGames();
        }

        /// <summary>
        /// Sends request to master server to find games list
        /// </summary>
        public void FindGames()
        {
            ClearGamesList();

            canvasGroup.interactable = false;

            if (statusInfoText)
            {
                statusInfoText.text = "Finding rooms... Please wait!";
                statusInfoText.gameObject.SetActive(true);
            }

            MstTimer.WaitForSeconds(0.2f, () =>
            {
                Mst.Client.Matchmaker.FindGames((games) =>
                {
                    canvasGroup.interactable = true;

                    if (games.Count == 0)
                    {
                        statusInfoText.text = "No games found! Try to create your own.";
                        return;
                    }

                    statusInfoText.gameObject.SetActive(false);
                    DrawGamesList(games);
                });
            });
        }

        private void DrawGamesList(IEnumerable<GameInfoPacket> games)
        {
            if (listContainer && gameItemPrefab)
            {
                foreach (GameInfoPacket game in games)
                {
                    var gameItemInstance = Instantiate(gameItemPrefab, listContainer, false);
                    gameItemInstance.SetInfo(game, this);

                    Logs.Info(game);
                }
            }
            else
            {
                Logs.Error("Not all components are setup");
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
    }
}