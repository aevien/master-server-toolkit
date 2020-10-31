using System;
using System.Collections;
using System.Collections.Generic;
using KskGroup;
using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class PingModule : BaseServerModule
    {
        [SerializeField, TextArea(3, 5)]
        private string pongMessage = "Hello, Pong!";

        public override void Initialize(IServer server)
        {
            server.RegisterMessageHandler((short)MstMessageCodes.Ping, OnPingRequestListener);
        }

        private void OnPingRequestListener(IIncomingMessage message)
        {
            JObject json = new JObject()
            {
                { "name","Vladimir" }
            };

            message.Respond(JsonConvert.SerializeObject(new AccountInfo()).ToBytes(), ResponseStatus.Success);
        }
    }
}