using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        public override bool IsConnected
        {
            get { return socket.IsConnected; }
        }

        public void SendDelayedMessages()
        {
            MstTimer.Singleton.StartCoroutine(SendDelayedMessagesCoroutine());
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

            socket.Send(message.ToBytes());
        }

        public override void Disconnect(string reason)
        {
            socket.Close(reason);
        }
    }
}