using System;
using System.Collections;
using System.Collections.Generic;
using MasterServerToolkit.Networking;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class PingModule : BaseServerModule
    {
        [SerializeField, TextArea(3, 5)]
        private string pongMessage = "Hello, Pong!";

        public override void Initialize(IServer server)
        {
            server.SetHandler((short)MstMessageCodes.Ping, OnPingRequestListener);
        }

        private void OnPingRequestListener(IIncommingMessage message)
        {
            message.Respond(pongMessage.ToBytes(), ResponseStatus.Success);
        }
    }
}