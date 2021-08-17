using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicNetworking
{
    public class CustomClient : SingletonBehaviour<CustomClient>
    {
        // Start is called before the first frame update
        void Start()
        {
            Mst.Client.Connection.AddConnectionListener(Connection_OnConnectedEvent);
            Mst.Client.Connection.AddDisconnectionListener(Connection_OnDisconnectedEvent);
        }

        /// <summary>
        /// Sends message to server
        /// </summary>
        public void SendNetMessage()
        {
            string message = "Hello from client";
            Mst.Client.Connection.SendMessage((short)MessageCodes.Message, message);
        }

        /// <summary>
        /// Sends message to server and waiting for response
        /// </summary>
        public void SendNetMessageWithResponse()
        {
            string text = "Hello from client and waiting for response from server";
            Mst.Client.Connection.SendMessage((short)MessageCodes.MessageWithResponse, text, (status, message) =>
            {
                if (status == Networking.ResponseStatus.Error)
                {
                    Logs.Error(message.AsString());
                    return;
                }

                Logs.Info($"Client received response message from server: {message.AsString()}");
            });
        }

        /// <summary>
        /// Connection callback handler
        /// </summary>
        private void Connection_OnConnectedEvent()
        {
            Logs.Info("Now the client is ready to send messages to server and receive responses from it");
        }

        /// <summary>
        /// Disconnection callback
        /// </summary>
        private void Connection_OnDisconnectedEvent()
        {
            Logs.Info("Client disconnected from server");
        }
    }
}