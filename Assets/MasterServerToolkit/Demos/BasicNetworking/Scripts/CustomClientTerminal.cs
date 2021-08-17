using MasterServerToolkit.CommandTerminal;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicNetworking
{
    public class CustomClientTerminal : MonoBehaviour
    {
        private void Start()
        {
            Terminal.Shell.AddCommand("demo.message", SendNetMessage, 0, 0, "Sends message to custom server");
            Terminal.Shell.AddCommand("demo.message.response", SendNetMessageWithResponse, 0, 0, "Sends message to custom server");
        }

        private void SendNetMessage(CommandArg[] args)
        {
            CustomClient.Singleton.SendNetMessage();
        }

        private void SendNetMessageWithResponse(CommandArg[] obj)
        {
            CustomClient.Singleton.SendNetMessageWithResponse();
        }
    }
}
