using MasterServerToolkit.Logging;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace arebones.Networking
{
    public class EchoService : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            Logs.Debug($"Message size: {e.RawData.LongLength}. The number of concurrent clients is: {Sessions.Count}");

            Send(e.Data);
        }
    }
}
