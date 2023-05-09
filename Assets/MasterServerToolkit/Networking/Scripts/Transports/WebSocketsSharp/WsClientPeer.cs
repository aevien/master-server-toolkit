using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;

namespace MasterServerToolkit.Networking
{
    public class WsClientPeer : BasePeer
    {
        private readonly WebSocket socket;
        private Queue<byte[]> delayedMessages;
        private readonly float delay = 0.2f;

        public WsClientPeer(WebSocket socket)
        {
            this.socket = socket;
            delayedMessages = new Queue<byte[]>();
        }

        public override bool IsConnected => socket != null ? socket.IsConnected : false;

        public void SendDelayedMessages()
        {
            SafeCoroutine.PermanentRunner.StartCoroutine(SendDelayedMessagesCoroutine());
        }

        public IEnumerator SendDelayedMessagesCoroutine()
        {
            yield return new WaitForSecondsRealtime(delay);

            if (delayedMessages == null)
            {
                yield break;
            }

            lock (delayedMessages)
            {
                if (delayedMessages == null)
                {
                    yield break;
                }

                var copy = delayedMessages;
                delayedMessages = null;

                foreach (var data in copy)
                {
                    socket.Send(data);
                }
            }
        }

        public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
        {
            if (delayedMessages != null)
            {
                lock (delayedMessages)
                {
                    if (delayedMessages != null)
                    {
                        delayedMessages.Enqueue(message.ToBytes());
                        return;
                    }
                }
            }

            Mst.TrafficStatistics.RegisterOpCodeTrafic(message.OpCode, message.Data.LongLength, TrafficType.Outgoing);
            socket.Send(message.ToBytes());
        }

        public override void Disconnect(string reason)
        {
            Disconnect((ushort)CloseStatusCode.Normal, reason);
        }

        public override void Disconnect(ushort code, string reason)
        {
            socket.Close(code, reason);
        }

        public void Connect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SafeCoroutine.Runner.StartCoroutine(socket.Connect());
#else
            socket.Connect();
#endif
        }
    }
}