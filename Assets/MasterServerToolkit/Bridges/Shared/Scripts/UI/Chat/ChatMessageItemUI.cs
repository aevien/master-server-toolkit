using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class ChatMessageItemUI : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        private TMP_Text senderNameText;
        [SerializeField]
        private TMP_Text messageText;

        public void Set(string sender, string message)
        {
            if (senderNameText)
                senderNameText.text = sender;

            if (messageText)
                messageText.text = message;
        }
    }
}