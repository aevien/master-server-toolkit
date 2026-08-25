using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class ChatChannelItemUI : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        [Tooltip("Reserved channel icon Image. The current Repaint implementation does not modify this reference.")]
        private Image iconImage;
        [SerializeField]
        [Tooltip("Required text label that displays the channel display name.")]
        private TMP_Text titleText;
        [SerializeField]
        [Tooltip("Required text label that displays the current number of users in the channel.")]
        private TMP_Text onlineText;

        public string DsplayName { get; set; }

        public int OnlineCount { get; set; }

        public void Repaint()
        {
            //iconImage.sprite = null;
            titleText.text = $"{DsplayName}";
            onlineText.text = $"Users online: {OnlineCount}";
        }
    }
}
