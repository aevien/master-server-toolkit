using MasterServerToolkit.MasterServer;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.Games
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