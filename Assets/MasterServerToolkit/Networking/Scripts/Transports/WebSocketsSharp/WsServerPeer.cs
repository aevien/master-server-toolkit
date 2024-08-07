﻿using MasterServerToolkit.MasterServer;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebSocketSharp;

namespace MasterServerToolkit.Networking
{
    public class WsServerPeer : BasePeer
    {
        private readonly WsService serviceForPeer;
        private Queue<byte[]> delayedMessages;

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

            delayedMessages = new Queue<byte[]>();
        }

        private void ServiceForPeer_OnMessageEvent(byte[] data)
        {
            HandleReceivedData(data);
        }

        private void ServiceForPeer_OnOpenEvent()
        {
            _ = SendDelayedMessages();
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
            if (serviceForPeer != null)
            {
                serviceForPeer.OnOpenEvent -= ServiceForPeer_OnOpenEvent;
                serviceForPeer.OnCloseEvent -= ServiceForPeer_OnCloseEvent;
                serviceForPeer.OnErrorEvent -= ServiceForPeer_OnErrorEvent;
                serviceForPeer.OnMessageEvent -= ServiceForPeer_OnMessageEvent;
                serviceForPeer.Dispose();
            }

            delayedMessages?.Clear();

            base.Dispose(disposing);
        }

        private Task SendDelayedMessages()
        {
            return Task.Run(async () =>
            {
                await Task.Delay(200);

                if (delayedMessages == null)
                {
                    logger.Error("Delayed messages are already sent");
                    return;
                }

                lock (delayedMessages)
                {
                    if (delayedMessages == null)
                        return;

                    var delayedMessagesCopy = delayedMessages;
                    delayedMessages = null;

                    foreach (var data in delayedMessagesCopy)
                        serviceForPeer.SendAsync(data);
                }
            });
        }

        public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
        {
            if (IsConnected)
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

                Mst.TrafficStatistics.RegisterOpCodeTrafic(message.OpCode, message.Data.LongLength, TrafficType.Outgoing);
                serviceForPeer.SendAsync(message.ToBytes());
            }
            else
            {
                logger.Error($"Server is trying to send data to peer {Id}, but it is not connected");
            }
        }

        public override void Disconnect(string reason = "")
        {
            Disconnect((ushort)CloseStatusCode.Normal, reason);
        }

        public override void Disconnect(ushort code, string reason = "")
        {
            serviceForPeer.Disconnect(code, reason);
        }
    }
}