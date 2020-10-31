using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    public class WsServerPeer : BasePeer
    {
        private readonly WsService _session;
        private bool _isConnected;
        private Queue<byte[]> _delayedMessages;

        public WsServerPeer(WsService session)
        {
            _session = session;

            _session.OnOpenEvent += () => { _isConnected = true; };
            _session.OnCloseEvent += (msg) => { _isConnected = false; };
            _session.OnErrorEvent += (msg) => { _isConnected = false; };

            _delayedMessages = new Queue<byte[]>();

            // When we're creating a peer in server, it's considered that there's 
            // already a connection for which we're making it.
            _isConnected = true;
        }

        public IEnumerator SendDelayedMessages(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (_delayedMessages == null)
            {
                Debug.LogError("Delayed messages are already sent");
                yield break;
            }

            lock (_delayedMessages)
            {
                if (_delayedMessages == null)
                {
                    yield break;
                }

                var copy = _delayedMessages;
                _delayedMessages = null;

                foreach (var data in copy)
                {
                    _session.SendData(data);
                }
            }
        }

        public override bool IsConnected => _isConnected;

        public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
        {
            if (_delayedMessages != null)
            {
                lock (_delayedMessages)
                {
                    if (_delayedMessages != null)
                    {
                        _delayedMessages.Enqueue(message.ToBytes());
                        return;
                    }
                }
            }

            _session.SendData(message.ToBytes());
        }

        public override void Disconnect(string reason)
        {
            _session.Disconnect();
        }
    }
}