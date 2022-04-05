using System;
using System.Collections;
using System.Collections.Generic;
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