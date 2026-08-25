using MasterServerToolkit.MasterServer;
using System;
#if !UNITY_WEBGL || UNITY_EDITOR
using WebSocketSharp;

namespace MasterServerToolkit.Networking
{
    public class WsServerPeer : BasePeer
    {
        private readonly WsService serviceForPeer;
        public override bool IsConnected => serviceForPeer != null 
            && serviceForPeer.ReadyState == WebSocketState.Open;

        public WsServerPeer(WsService session) : base()
        {
            serviceForPeer = session;
            serviceForPeer.IgnoreExtensions = true;
            serviceForPeer.OnOpenEvent += ServiceForPeer_OnOpenEvent;
            serviceForPeer.OnCloseEvent += ServiceForPeer_OnCloseEvent;
            serviceForPeer.OnErrorEvent += ServiceForPeer_OnErrorEvent;
            serviceForPeer.OnMessageEvent += ServiceForPeer_OnMessageEvent;
        }

        private void ServiceForPeer_OnMessageEvent(byte[] data)
        {
            HandleReceivedData(data);
        }

        private void ServiceForPeer_OnOpenEvent()
        {
            NotifyConnectionOpenEvent();
        }

        private void ServiceForPeer_OnCloseEvent(ushort code, string reason)
        {
            NotifyConnectionCloseEvent(code, reason);
        }

        private void ServiceForPeer_OnErrorEvent(string error)
        {
            logger.Error(error);
            NotifyConnectionCloseEvent((ushort)CloseStatusCode.Abnormal, error);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (serviceForPeer != null)
                {
                    serviceForPeer.OnOpenEvent -= ServiceForPeer_OnOpenEvent;
                    serviceForPeer.OnCloseEvent -= ServiceForPeer_OnCloseEvent;
                    serviceForPeer.OnErrorEvent -= ServiceForPeer_OnErrorEvent;
                    serviceForPeer.OnMessageEvent -= ServiceForPeer_OnMessageEvent;
                    serviceForPeer.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
        {
            if (IsConnected)
            {
                Mst.Traffic.RegisterOpCodeTrafic(message.OpCode, message.Data.LongLength, TrafficType.Outgoing);
                serviceForPeer.SendAsync(message.ToBytes());
            }
            else
            {
                logger.Error($"Server is trying to send data to peer {Id}, but it is not connected");
            }
        }

        protected override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod,
            Action<bool> completionCallback)
        {
            if (!IsConnected)
            {
                completionCallback?.Invoke(false);
                return;
            }

            Mst.Traffic.RegisterOpCodeTrafic(message.OpCode, message.Data.LongLength, TrafficType.Outgoing);
            serviceForPeer.SendAsync(message.ToBytes(), completionCallback);
        }

        public override void Disconnect(string reason = "")
        {
            Disconnect((ushort)CloseStatusCode.Normal, reason);
        }

        public override void Disconnect(ushort code, string reason = "")
        {
            serviceForPeer.Disconnect(code, reason);
            BeginDisconnect();
        }
    }
}

#endif
