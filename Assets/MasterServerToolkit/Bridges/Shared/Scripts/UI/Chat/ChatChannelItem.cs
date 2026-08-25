using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class ChatChannelItem : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        [Tooltip("Required Image whose sprite is cleared when channel information is assigned.")]
        private Image iconImage;
        [SerializeField]
        [Tooltip("Required text label that displays the channel name and current online-user count.")]
        private TMP_Text titleText;

        public void Set(ChatChannelInfo channelInfo)
        {
            iconImage.sprite = null;
            titleText.text = $"{channelInfo.Name} ({channelInfo.OnlineCount})";
        }
    }
}
