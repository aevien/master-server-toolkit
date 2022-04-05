using MasterServerToolkit.Logging;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MasterServerToolkit.Networking
{
    public class EchoService : WebSocketServiceBehavior
    {
        protected override void OnOpen()
        {
            Debug.Log($"New client connected. The number of concurrent clients is: {Sessions.Count}");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Logs.Info($"Message size: {e.RawData.LongLength / 1024f}kb.");

            if (ConnectionState == WebSocketState.Open)
                SendAsync(e.Data, (isSuccess) =>
                {
                    if (!isSuccess)
                        Logs.Error("Response did not send");
                });
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            Logs.Info($"<color=#00FF00>Connection closed. Reason: [{e.Reason}], WasClean: [{e.WasClean}]</color>");
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
            Logs.Error(e.Message);
        }
    }
}
