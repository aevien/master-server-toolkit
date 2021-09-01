using MasterServerToolkit.Logging;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    public class WsServerPeer : BasePeer
    {
        private readonly WsService serviceForPeer;
        private Queue<byte[]> delayedMessages;
        private readonly float delay = 0.2f;
        private bool isConnected = false;

        public WsServerPeer(WsService session)
        {
            serviceForPeer = session;

            serviceForPeer.OnOpenEvent += () => { isConnected = true; };
            serviceForPeer.OnCloseEvent += (msg) => { isConnected = false; };
            serviceForPeer.OnErrorEvent += (msg) => { isConnected = false; };

            delayedMessages = new Queue<byte[]>();

            isConnected = true;
        }

        public void SendDelayedMessages()
        {
            MstTimer.Singleton.StartCoroutine(SendDelayedMessagesCoroutine());
        }

        private IEnumerator SendDelayedMessagesCoroutine()
        {
            yield return new WaitForSecondsRealtime(delay);

            if (delayedMessages == null)
            {
                Debug.LogError("Delayed messages are already sent");
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
                    serviceForPeer.SendAsync(data);
                }
            }
        }

        public override bool IsConnected => isConnected;

        public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
        {
            if(serviceForPeer.ConnectionState == WebSocketSharp.WebSocketState.Open)
            {
                // There's a bug in websockets
                // When server sends a message to client right after client
                // connects to it, the message is lost somewhere.
                // Sending first few messages with a small delay fixes this issue.

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

                serviceForPeer.SendAsync(message.ToBytes());
            }
            else
            {
                Logs.Error($"Server is trying to send data to peer {Id}, but it is not connected");
            }
        }

        public override void Disconnect(string reason)
        {
            serviceForPeer.CloseAsync(reason);
        }
    }
}