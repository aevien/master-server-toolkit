using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// This is an abstract implementation of <see cref="IPeer" /> interface,
    /// which handles acknowledgements and SendMessage overloads.
    /// Extend this, if you want to implement custom protocols
    /// </summary>
    public abstract class BasePeer : IPeer
    {
        private readonly Dictionary<int, ResponseCallback> acknowledgements;
        protected readonly List<long[]> ackTimeoutQueue;
        private readonly Dictionary<int, object> peerPropertyData;
        private int _id = -1;
        private int nextAckId = 1;
        private readonly IIncomingMessage timeoutMessage;
        private readonly Dictionary<Type, IPeerExtension> extensionsList;
        private static readonly object idGenerationLock = new object();
        private static int peerIdGenerator;

        /// <summary>
        /// Default timeout, after which response callback is invoked with
        /// timeout status.
        /// </summary>
        public static int DefaultTimeoutSecs { get; set; } = 60;

        /// <summary>
        /// True, if connection is still valid
        /// </summary>
        public abstract bool IsConnected { get; }

        protected BasePeer()
        {
            peerPropertyData = new Dictionary<int, object>();
            acknowledgements = new Dictionary<int, ResponseCallback>(30);
            ackTimeoutQueue = new List<long[]>();
            extensionsList = new Dictionary<Type, IPeerExtension>();

            MstTimer.Singleton.OnTickEvent += HandleAckDisposalTick;

            timeoutMessage = new IncomingMessage(-1, 0, "Time out".ToBytes(), DeliveryMethod.ReliableFragmentedSequenced, this)
            {
                Status = ResponseStatus.Timeout
            };
        }

        /// <summary>
        /// Fires when peer received message
        /// </summary>
        public event Action<IIncomingMessage> OnMessageReceivedEvent;

        /// <summary>
        /// Fires when peer disconnects
        /// </summary>
        public event PeerActionHandler OnPeerDisconnectedEvent;

        /// <summary>
        /// Current peer info
        /// </summary>
        public IPeer Peer { get; private set; }

        /// <summary>
        /// Unique peer id
        /// </summary>
        public int Id
        {
            get
            {
                if (_id < 0)
                    lock (idGenerationLock)
                    {
                        if (_id < 0)
                            _id = peerIdGenerator++;
                    }

                return _id;
            }
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        public void SendMessage(short opCode)
        {
            SendMessage(MessageHelper.Create(opCode), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        public void SendMessage(short opCode, ISerializablePacket packet)
        {
            SendMessage(MessageHelper.Create(opCode, packet), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <param name="method"></param>
        public void SendMessage(short opCode, ISerializablePacket packet, DeliveryMethod method)
        {
            SendMessage(MessageHelper.Create(opCode, packet), method);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <param name="responseCallback"></param>
        public void SendMessage(short opCode, ISerializablePacket packet, ResponseCallback responseCallback)
        {
            var message = MessageHelper.Create(opCode, packet.ToBytes());
            SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public void SendMessage(short opCode, ISerializablePacket packet, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, packet.ToBytes());
            SendMessage(message, responseCallback, timeoutSecs, DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="responseCallback"></param>
        public void SendMessage(short opCode, ResponseCallback responseCallback)
        {
            SendMessage(MessageHelper.Create(opCode), responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        public void SendMessage(short opCode, byte[] data)
        {
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="ackCallback"></param>
        public void SendMessage(short opCode, byte[] data, ResponseCallback ackCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, ackCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public void SendMessage(short opCode, byte[] data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        public void SendMessage(short opCode, string data)
        {
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        public void SendMessage(short opCode, string data, ResponseCallback responseCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public void SendMessage(short opCode, string data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        public void SendMessage(short opCode, int data)
        {
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        public void SendMessage(short opCode, int data, ResponseCallback responseCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public void SendMessage(short opCode, int data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(IOutgoingMessage message)
        {
            SendMessage(message, DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <returns></returns>
        public int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback)
        {
            return SendMessage(message, responseCallback, DefaultTimeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <returns></returns>
        public int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            return SendMessage(message, responseCallback, timeoutSecs, DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        public int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs, DeliveryMethod deliveryMethod)
        {
            if (!IsConnected)
            {
                responseCallback.Invoke(ResponseStatus.NotConnected, null);
                return -1;
            }

            var id = RegisterAck(message, responseCallback, timeoutSecs);
            SendMessage(message, deliveryMethod);
            return id;
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        public abstract void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod);

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseCallback"></param>
        void IMsgDispatcher.SendMessage(IOutgoingMessage message, ResponseCallback responseCallback)
        {
            SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        void IMsgDispatcher.SendMessage(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Saves data into peer
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void SetProperty(int id, object data)
        {
            if (this.peerPropertyData.ContainsKey(id))
            {
                this.peerPropertyData[id] = data;
            }
            else
            {
                this.peerPropertyData.Add(id, data);
            }
        }

        /// <summary>
        /// Retrieves data from peer, which was stored with <see cref="SetProperty" />
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public object GetProperty(int id)
        {
            peerPropertyData.TryGetValue(id, out object value);
            return value;
        }

        /// <summary>
        /// Retrieves data from peer, which was stored with <see cref="SetProperty" />
        /// </summary>
        /// <param name="id"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public object GetProperty(int id, object defaultValue)
        {
            var obj = GetProperty(id);
            return obj ?? defaultValue;
        }

        /// <summary>
        /// Add any <see cref="IPeerExtension"/> to peer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="extension"></param>
        /// <returns></returns>
        public T AddExtension<T>(T extension) where T : IPeerExtension
        {
            extensionsList[typeof(T)] = extension;
            return extension;
        }

        /// <summary>
        /// Get any <see cref="IPeerExtension"/> from peer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetExtension<T>() where T : IPeerExtension
        {
            if (HasExtension<T>())
            {
                return (T)extensionsList[typeof(T)];
            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// Check if this peer has extension
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool HasExtension<T>()
        {
            return extensionsList.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Force disconnection
        /// </summary>
        /// <param name="reason"></param>
        public abstract void Disconnect(string reason);

        /// <summary>
        /// Notify OnPeerDisconnectedEvent
        /// </summary>
        public void NotifyDisconnectEvent()
        {
            OnPeerDisconnectedEvent?.Invoke(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        protected void NotifyMessageEvent(IIncomingMessage message)
        {
            OnMessageReceivedEvent?.Invoke(message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        /// <returns></returns>
        protected int RegisterAck(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            int id;

            lock (acknowledgements)
            {
                id = nextAckId++;
                acknowledgements.Add(id, responseCallback);
            }

            message.AckRequestId = id;

            StartAckTimeout(id, timeoutSecs);
            return id;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ackId"></param>
        /// <param name="statusCode"></param>
        /// <param name="message"></param>
        protected void TriggerAck(int ackId, ResponseStatus statusCode, IIncomingMessage message)
        {
            ResponseCallback ackCallback;
            lock (acknowledgements)
            {
                acknowledgements.TryGetValue(ackId, out ackCallback);

                if (ackCallback == null)
                {
                    return;
                }

                acknowledgements.Remove(ackId);
            }
            ackCallback(statusCode, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ackId"></param>
        /// <param name="timeoutSecs"></param>
        private void StartAckTimeout(int ackId, int timeoutSecs)
        {
            // +1, because it might be about to tick in a few miliseconds
            ackTimeoutQueue.Add(new[] { ackId, MstTimer.Singleton.CurrentTick + timeoutSecs + 1 });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public virtual void HandleMessage(IIncomingMessage message)
        {
            NotifyMessageEvent(message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        public void HandleDataReceived(byte[] buffer)
        {
            HandleDataReceived(buffer, 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="start"></param>
        public void HandleDataReceived(byte[] buffer, int start)
        {
            IIncomingMessage message;

            try
            {
                // Deserialize message from bytes
                message = MessageHelper.FromBytes(buffer, start, this);

                if (message.AckRequestId.HasValue)
                {
                    // We received a message which is a response to our ack request
                    TriggerAck(message.AckRequestId.Value, message.Status, message);
                    return;
                }
            }
            catch (Exception e)
            {
                Logs.Error("Failed parsing an incomming message: " + e);
                return;
            }

            HandleMessage(message);
        }

        #region Ack Disposal Stuff

        /// <summary>
        /// Called when ack disposal thread ticks
        /// </summary>
        private void HandleAckDisposalTick(long currentTick)
        {
            // TODO test with ordered queue, might be more performant
            ackTimeoutQueue.RemoveAll(a =>
            {
                if (a[1] > currentTick)
                {
                    return false;
                }

                try
                {
                    CancelAck((int)a[0], ResponseStatus.Timeout);
                }
                catch (Exception e)
                {
                    Logs.Error(e);
                }

                return true;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ackId"></param>
        /// <param name="responseCode"></param>
        private void CancelAck(int ackId, ResponseStatus responseCode)
        {
            ResponseCallback ackCallback;
            lock (acknowledgements)
            {
                acknowledgements.TryGetValue(ackId, out ackCallback);

                if (ackCallback == null)
                {
                    return;
                }

                acknowledgements.Remove(ackId);
            }
            ackCallback(responseCode, timeoutMessage);
        }

        #endregion

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    MstTimer.Singleton.OnTickEvent -= HandleAckDisposalTick;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}