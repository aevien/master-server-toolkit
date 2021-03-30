using MasterServerToolkit.Logging;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    public class WsServerPeer : BasePeer
    {
        private readonly WsService serviceForPeer;
        //private Queue<byte[]> _delayedMessages;
        private bool _isConnected = false;

        public WsServerPeer(WsService session)
        {
            serviceForPeer = session;

            serviceForPeer.OnOpenEvent += () => { _isConnected = true; };
            serviceForPeer.OnCloseEvent += (msg) => { _isConnected = false; };
            serviceForPeer.OnErrorEvent += (msg) => { _isConnected = false; };

            //_delayedMessages = new Queue<byte[]>();

            _isConnected = true;
        }

        //public IEnumerator SendDelayedMessages(float delay)
        //{
        //    yield return new WaitForSecondsRealtime(delay);

        //    if (_delayedMessages == null)
        //    {
        //        Debug.LogError("Delayed messages are already sent");
        //        yield break;
        //    }

        //    lock (_delayedMessages)
        //    {
        //        if (_delayedMessages == null)
        //        {
        //            yield break;
        //        }

        //        var copy = _delayedMessages;
        //        _delayedMessages = null;

        //        foreach (var data in copy)
        //        {
        //            _session.SendMessage(data);
        //        }
        //    }
        //}

        public override bool IsConnected => _isConnected;

        public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
        {
            if(serviceForPeer.ConnectionState == WebSocketSharp.WebSocketState.Open)
            {
                //if (_delayedMessages != null)
                //{
                //    lock (_delayedMessages)
                //    {
                //        if (_delayedMessages != null)
                //        {
                //            _delayedMessages.Enqueue(message.ToBytes());
                //            return;
                //        }
                //    }
                //}

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