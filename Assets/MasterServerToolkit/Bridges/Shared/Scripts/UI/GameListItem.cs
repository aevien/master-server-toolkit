using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class GameListItem : MonoBehaviour
    {
        #region INSPECTOR

        [SerializeField]
        [Tooltip("Optional label that displays the room name and a password-protected marker when applicable.")]
        private TextMeshProUGUI gameNameText;

        [SerializeField]
        [Tooltip("Optional label that displays the room network address.")]
        private TextMeshProUGUI gameAddressText;

        [SerializeField]
        [Tooltip("Optional label that displays the room region, using International when the region is empty.")]
        private TextMeshProUGUI gameRegionText;

        [SerializeField]
        [Tooltip("Optional label that displays current and maximum players. A maximum of 0 or less is shown as unlimited.")]
        private TextMeshProUGUI gamePlayersText;

        [SerializeField]
        [Tooltip("Optional button configured to start a match with the room represented by this list item.")]
        private Button connectButton;

        #endregion

        public void SetInfo(GameInfoPacket gameInfo, GamesListView owner)
        {
            if (gameNameText)
            {
                gameNameText.text = gameInfo.IsPasswordProtected ? $"{gameInfo.Name} <color=yellow>[Password]</color>" : gameInfo.Name;
            }

            if (gameAddressText)
            {
                gameAddressText.text = gameInfo.Address;
            }

            if (gameRegionText)
            {
                string region = string.IsNullOrEmpty(gameInfo.Region) ? "International" : gameInfo.Region;
                gameRegionText.text = $"Region: <color=yellow>{region}</color>";
            }

            if (gamePlayersText)
            {
                string maxPleyers = gameInfo.MaxPlayers <= 0 ? "∞" : gameInfo.MaxPlayers.ToString();
                gamePlayersText.text = $"Players: <color=yellow>{gameInfo.OnlinePlayers} / {maxPleyers}</color>";
            }

            if (connectButton)
            {
                connectButton.onClick.RemoveAllListeners();
                connectButton.onClick.AddListener(() =>
                {
                    MatchmakingBehaviour.Instance.StartMatch(gameInfo);
                });
            }
        }
    }
}
