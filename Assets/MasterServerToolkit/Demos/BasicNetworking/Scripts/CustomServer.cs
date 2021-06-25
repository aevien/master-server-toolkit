using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicNetworking
{
    public class CustomServer : ServerBehaviour
    {
        protected override void OnStartedServer()
        {
            base.OnStartedServer();

            RegisterMessageHandler((short)MessageCodes.Message, OnMessageReceivedHandler);
            RegisterMessageHandler((short)MessageCodes.MessageWithResponse, OnMessageWithResponseReceivedHandler);
        }

        private void OnMessageWithResponseReceivedHandler(IIncomingMessage message)
        {
            Logs.Info($"Server received messge from client: {message.AsString()}. Sending response to client");
            message.Respond("Hello from server!");
        }

        private void OnMessageReceivedHandler(IIncomingMessage message)
        {
            Logs.Info($"Server received messge from client: {message.AsString()}");
        }
    }

    public enum MessageCodes
    {
        Message,
        MessageWithResponse
    }
}