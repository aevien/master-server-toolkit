using MasterServerToolkit.Extensions;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MasterServerToolkit.Networking
{
    public class EchoService : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            Debug.Log($"New client connected. The number of concurrent clients is: {Sessions.Count}");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Mst.TrafficStatistics.RegisterGenericTrafic(e.RawData.LongLength, TrafficType.Incoming);

            Logs.Info($"Message size: {e.RawData.LongLength / 1024f}kb.");

            if (ReadyState == WebSocketState.Open)
            {
                Mst.TrafficStatistics.RegisterGenericTrafic(e.RawData.LongLength, TrafficType.Outgoing);

                SendAsync(e.Data, (isSuccess) =>
                {
                    if (!isSuccess)
                        Logs.Error("Response did not send");
                });
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            Logs.Info($"Connection closed. Reason: [{e.Reason}], WasClean: [{e.WasClean}]".ToRed());
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
            Logs.Error(e.Message);
            CloseAsync();
        }
    }
}
