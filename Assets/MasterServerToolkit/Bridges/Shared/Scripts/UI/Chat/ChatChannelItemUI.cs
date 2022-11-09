using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class ChatChannelItemUI : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        private Image iconImage;
        [SerializeField]
        private TMP_Text titleText;
        [SerializeField]
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