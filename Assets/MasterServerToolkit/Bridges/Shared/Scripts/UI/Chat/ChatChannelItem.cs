using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class ChatChannelItem : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        private Image iconImage;
        [SerializeField]
        private TMP_Text titleText;

        public void Set(ChatChannelInfo channelInfo)
        {
            iconImage.sprite = null;
            titleText.text = $"{channelInfo.Name} ({channelInfo.OnlineCount})";
        }
    }
}