using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    public class MessageItem : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        private TMP_Text messageText;

        public void Set(string sender, string message)
        {
            messageText.text = $"<b><color=#A2DC09>{sender}</color></b>\n{message}";
        }
    }
}