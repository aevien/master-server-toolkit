using System;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicTelegramBotLogger
{
    public class TelegramBotDemo : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                throw new NullReferenceException("This object cannot be found");
            }
        }
    }
}