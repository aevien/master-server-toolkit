using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

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
        private readonly Dictionary<int, object> data;
        private int _id = -1;
        private int nextAckId = 1;
        private readonly IIncommingMessage timeoutMessage;
        private readonly Dictionary<Type, IPeerExtension> extensionsList;
        private static readonly object idGenerationLock = new object();
        private static int peerIdGenerator;

        /// <summary>
        /// Default timeout, after which response callback is invoked with
        /// timeout status.
        /// </summary>
        public static int DefaultTimeoutSecs { get; set; } = 60;


        public static bool DontCatchExceptionsInEditor { get; set; } = true;

        /// <summary>
        /// True, if connection is stil valid
        /// </summary>
        public abstract bool IsConnected { get; }

        protected BasePeer()
        {
            data = new Dictionary<int, object>();
            acknowledgements = new Dictionary<int, ResponseCallback>(30);
            ackTimeoutQueue = new List<long[]>();
            extensionsList = new Dictionary<Type, IPeerExtension>();

            MstTimer.Instance.OnTickEvent += HandleAckDisposalTick;

            timeoutMessage = new IncommingMessage(-1, 0, "Time out".ToBytes(), DeliveryMethod.Reliable, this)
            {
                Status = ResponseStatus.Timeout
            };
        }

        /// <summary>
        /// Fires when peer received message
        /// </summary>
        public event Action<IIncommingMessage> OnMessageReceivedEvent;

        /// <summary>
        /// Fires when peer disconnects
        /// </summary>
        public event PeerActionHandler OnPeerDisconnectedEvent;

        /// <summary>
        /// Current peer info
        /// </summary>
        public IPeer Peer { get; private set; }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        public void SendMessage(short opCode)
        {
            SendMessage(MessageHelper.Create(opCode), DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        public void SendMessage(short opCode, ISerializablePacket packet)
        {
            SendMessage(MessageHelper.Create(opCode, packet), DeliveryMethod.Reliable);
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
            SendMessage(message, responseCallback, timeoutSecs, DeliveryMethod.Reliable);
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
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.Reliable);
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
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.Reliable);
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
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.Reliable);
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
        public void SendMessage(IMessage message)
        {
            SendMessage(message, DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <returns></returns>
        public int SendMessage(IMessage message, ResponseCallback responseCallback)
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
        public int SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            return SendMessage(message, responseCallback, timeoutSecs, DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        public int SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs, DeliveryMethod deliveryMethod)
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
        public abstract void SendMessage(IMessage message, DeliveryMethod deliveryMethod);

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseCallback"></param>
        void IMsgDispatcher<IPeer>.SendMessage(IMessage message, ResponseCallback responseCallback)
        {
            SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        void IMsgDispatcher<IPeer>.SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs)
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
            if (this.data.ContainsKey(id))
            {
                this.data[id] = data;
            }
            else
            {
                this.data.Add(id, data);
            }
        }

        /// <summary>
        /// Retrieves data from peer, which was stored with <see cref="SetProperty" />
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public object GetProperty(int id)
        {
            data.TryGetValue(id, out object value);
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

        public bool HasExtension<T>()
        {
            return extensionsList.ContainsKey(typeof(T));
        }

        public abstract void Disconnect(string reason);

        public void NotifyDisconnectEvent()
        {
            OnPeerDisconnectedEvent?.Invoke(this);
        }

        protected void NotifyMessageEvent(IIncommingMessage message)
        {
            OnMessageReceivedEvent?.Invoke(message);
        }

        protected int RegisterAck(IMessage message, ResponseCallback responseCallback, int timeoutSecs)
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

        protected void TriggerAck(int ackId, ResponseStatus statusCode, IIncommingMessage message)
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

        private void StartAckTimeout(int ackId, int timeoutSecs)
        {
            // +1, because it might be about to tick in a few miliseconds
            ackTimeoutQueue.Add(new[] { ackId, MstTimer.Instance.CurrentTick + timeoutSecs + 1 });
        }

        public virtual void HandleMessage(IIncommingMessage message)
        {
            OnMessageReceivedEvent?.Invoke(message);
        }

        public void HandleDataReceived(byte[] buffer, int start)
        {
            IIncommingMessage message = null;

            try
            {
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
#if UNITY_EDITOR
                if (DontCatchExceptionsInEditor)
                    throw e;
#endif
                Debug.LogError("Failed parsing an incomming message: " + e);

                return;
            }

            HandleMessage(message);
        }

        #region Ack Disposal Stuff

        public int Id
        {
            get
            {
                if (_id < 0)
                {
                    lock (idGenerationLock)
                    {
                        if (_id < 0)
                        {
                            _id = peerIdGenerator++;
                        }
                    }
                }

                return _id;
            }
        }

        private void HandleAckDisposalTick(long currentTick)
        {
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
                    MstTimer.Instance.OnTickEvent -= HandleAckDisposalTick;
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