using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class ChatMessageItemUI : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        [Tooltip("Optional text label populated with the message sender name.")]
        private TMP_Text senderNameText;
        [SerializeField]
        [Tooltip("Optional text label populated with the chat message body.")]
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
