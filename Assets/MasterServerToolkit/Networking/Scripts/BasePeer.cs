using MasterServerToolkit.Extensions;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// This is an abstract implementation of <see cref="IPeer" /> interface,
    /// which handles acknowledgements and SendMessage overloads.
    /// Extend this, if you want to implement custom protocols
    /// </summary>
    public abstract class BasePeer : IPeer, IMsgDispatcher
    {
        private enum PendingAckSendState
        {
            Registered,
            Sending,
            Sent
        }

        private sealed class PendingAck
        {
            public PendingAck(int id, IOutgoingMessage message, ResponseCallback callback, int timeoutSeconds,
                long deadlineTick, int? previousAckRequestId)
            {
                Id = id;
                Message = message;
                Callback = callback;
                TimeoutSeconds = timeoutSeconds;
                DeadlineTick = deadlineTick;
                PreviousAckRequestId = previousAckRequestId;
            }

            public int Id { get; }
            public IOutgoingMessage Message { get; set; }
            public ResponseCallback Callback { get; }
            public int TimeoutSeconds { get; }
            public long DeadlineTick { get; }
            public int? PreviousAckRequestId { get; }
            public PendingAckSendState SendState { get; set; }
        }

        private readonly object pendingAcksSync = new object();
        private readonly Dictionary<int, PendingAck> pendingAcks = new Dictionary<int, PendingAck>();
        private readonly ConcurrentDictionary<uint, object> peerPropertyData = new ConcurrentDictionary<uint, object>();
        private int _id = -1;
        private int nextAckId;
        private readonly ConcurrentDictionary<Type, IPeerExtension> extensionsList = new ConcurrentDictionary<Type, IPeerExtension>();
        private static readonly object idGenerationLock = new object();
        private static int peerIdGenerator;
        private bool acceptsAcks = true;
        private bool disposedValue = false;
        protected readonly Logger logger;
        protected LogLevel logLevel = LogLevel.Info;

        /// <summary>
        /// Default timeout, after which response callback is invoked with
        /// timeout status.
        /// </summary>
        public static int DefaultTimeoutSecs { get; set; } = 60;

        /// <summary>
        /// True, if connection is still valid
        /// </summary>
        public abstract bool IsConnected { get; }

        /// <summary>
        /// Fires when peer received message
        /// </summary>
        public event Action<IIncomingMessage> OnMessageReceivedEvent;

        /// <summary>
        /// Fires when peer disconnects
        /// </summary>
        public event PeerActionHandler OnConnectionOpenEvent;

        /// <summary>
        /// Fires when peer disconnects
        /// </summary>
        public event PeerActionHandler OnConnectionCloseEvent;

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
        /// 
        /// </summary>
        public LogLevel LogLevel
        {
            get
            {
                return logLevel;
            }
            set
            {
                logLevel = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public DateTime StartActivity { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public ushort CloseCode { get; protected set; }

        protected BasePeer()
        {
            Peer = this;

            StartActivity = DateTime.Now;
            LastActivity = DateTime.Now;

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            MstTimer.OnTickEvent += HandleAckDisposalTick;
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        public void SendMessage(ushort opCode)
        {
            SendMessage(MessageHelper.Create(opCode), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        public void SendMessage(ushort opCode, ISerializablePacket packet)
        {
            SendMessage(MessageHelper.Create(opCode, packet), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <param name="method"></param>
        public void SendMessage(ushort opCode, ISerializablePacket packet, DeliveryMethod method)
        {
            SendMessage(MessageHelper.Create(opCode, packet), method);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <param name="responseCallback"></param>
        public int SendMessage(ushort opCode, ISerializablePacket packet, ResponseCallback responseCallback)
        {
            var message = MessageHelper.Create(opCode, packet.ToBytes());
            return SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public int SendMessage(ushort opCode, ISerializablePacket packet, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, packet.ToBytes());
            return SendMessage(message, responseCallback, timeoutSecs, DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="responseCallback"></param>
        public int SendMessage(ushort opCode, ResponseCallback responseCallback)
        {
            return SendMessage(MessageHelper.Create(opCode), responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        public void SendMessage(ushort opCode, byte[] data)
        {
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="ackCallback"></param>
        public int SendMessage(ushort opCode, byte[] data, ResponseCallback ackCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            return SendMessage(message, ackCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public int SendMessage(ushort opCode, byte[] data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, data);
            return SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        public void SendMessage(ushort opCode, string data)
        {
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        public int SendMessage(ushort opCode, string data, ResponseCallback responseCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            return SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public int SendMessage(ushort opCode, string data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, data);
            return SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        public void SendMessage(ushort opCode, int data)
        {
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.ReliableFragmentedSequenced);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        public int SendMessage(ushort opCode, int data, ResponseCallback responseCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            return SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public int SendMessage(ushort opCode, int data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, data);
            return SendMessage(message, responseCallback, timeoutSecs);
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
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (responseCallback == null)
                throw new ArgumentNullException(nameof(responseCallback));

            PendingAck pendingAck = RegisterPendingAck(message, responseCallback, timeoutSecs);

            if (pendingAck == null)
                return -1;

            if (!TryBeginSending(pendingAck))
            {
                RestoreAckRequestId(message, pendingAck);
                return -1;
            }

            try
            {
                SendMessage(message, deliveryMethod, isSuccessful => HandleSendResult(pendingAck, isSuccessful));
            }
            catch
            {
                ResponseStatus failureStatus = IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected;
                CompletePendingAck(pendingAck, failureStatus, CreateTerminalResponse(failureStatus), true);
                throw;
            }

            return pendingAck.Id;
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        public abstract void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod);

        /// <summary>
        /// Sends a message and reports whether the transport accepted it.
        /// Derived transports with asynchronous completion should override this method.
        /// </summary>
        protected virtual void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod,
            Action<bool> completionCallback)
        {
            SendMessage(message, deliveryMethod);
            completionCallback?.Invoke(IsConnected);
        }

        /// <summary>
        /// Saves data into peer
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void SetProperty(uint id, object data)
        {
            peerPropertyData[id] = data;
        }

        /// <summary>
        /// Retrieves data from peer, which was stored with <see cref="SetProperty" />
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public object GetProperty(uint id)
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
        public object GetProperty(uint id, object defaultValue)
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
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool ClearExtension<T>() where T : IPeerExtension
        {
            if (HasExtension<T>())
            {
                extensionsList[typeof(T)] = null;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets <see cref="IPeerExtension"/> from peer
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
        /// Tries to get <see cref="IPeerExtension"/> from peer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="extension"></param>
        /// <returns></returns>
        public bool TryGetExtension<T>(out T extension) where T : IPeerExtension
        {
            extension = GetExtension<T>();
            return extension != null;
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
        public abstract void Disconnect(string reason = "");

        /// <summary>
        /// Force disconnection
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        public abstract void Disconnect(ushort code, string reason = "");

        /// <summary>
        /// Notify OnPeerDisconnectedEvent
        /// </summary>
        protected void NotifyConnectionOpenEvent()
        {
            OnConnectionOpenEvent?.Invoke(this);
            logger.Debug($"Peer [{Id}] opened connection");
        }

        /// <summary>
        /// Notify OnPeerDisconnectedEvent
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        protected void NotifyConnectionCloseEvent(ushort code, string reason = "")
        {
            CloseCode = code;
            BeginDisconnect();
            OnConnectionCloseEvent?.Invoke(this);
            logger.Debug($"Peer [{Id}] closed connection with code {code}. Reason is: {reason}");
        }

        /// <summary>
        /// Stops accepting acknowledgement requests and completes all pending requests.
        /// Call this before starting an asynchronous transport disconnect.
        /// </summary>
        protected void BeginDisconnect()
        {
            CompletePendingAcks(StopAcceptingAndDrainPendingAcks(), ResponseStatus.NotConnected);
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
            return RegisterPendingAck(message, responseCallback, timeoutSecs)?.Id ?? -1;
        }

        private PendingAck RegisterPendingAck(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (responseCallback == null)
                throw new ArgumentNullException(nameof(responseCallback));

            PendingAck pendingAck = null;
            bool isConnected = IsConnected;
            int? previousAckRequestId = message.AckRequestId;

            lock (pendingAcksSync)
            {
                if (acceptsAcks && !disposedValue && isConnected)
                {
                    int id = AllocateAckIdLocked();
                    long deadlineTick = MstTimer.CurrentTick + timeoutSecs + 1L;
                    pendingAck = new PendingAck(id, message, responseCallback, timeoutSecs, deadlineTick,
                        previousAckRequestId);
                    pendingAcks.Add(id, pendingAck);
                }
            }

            if (pendingAck == null)
            {
                responseCallback.Invoke(ResponseStatus.NotConnected, CreateTerminalResponse(ResponseStatus.NotConnected));
                return null;
            }

            try
            {
                message.AckRequestId = pendingAck.Id;
            }
            catch
            {
                CompletePendingAck(pendingAck, ResponseStatus.Error,
                    CreateTerminalResponse(ResponseStatus.Error), true);
                throw;
            }

            return pendingAck;
        }

        private void RestoreAckRequestId(IOutgoingMessage message, PendingAck pendingAck)
        {
            try
            {
                if (message.AckRequestId == pendingAck.Id)
                    message.AckRequestId = pendingAck.PreviousAckRequestId;
            }
            catch (Exception exception)
            {
                TryLogError($"Failed to restore outgoing message ACK id: {exception}");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ackId"></param>
        /// <param name="statusCode"></param>
        /// <param name="message"></param>
        protected void TriggerAck(int ackId, ResponseStatus statusCode, IIncomingMessage message)
        {
            CompletePendingAck(ackId, statusCode, message, true);
        }

        private int AllocateAckIdLocked()
        {
            do
            {
                nextAckId = nextAckId == int.MaxValue ? 1 : nextAckId + 1;
            }
            while (pendingAcks.ContainsKey(nextAckId));

            return nextAckId;
        }

        private bool TryBeginSending(PendingAck pendingAck)
        {
            lock (pendingAcksSync)
            {
                if (!pendingAcks.TryGetValue(pendingAck.Id, out PendingAck registeredAck) ||
                    !ReferenceEquals(registeredAck, pendingAck))
                    return false;

                pendingAck.SendState = PendingAckSendState.Sending;
                return true;
            }
        }

        private void HandleSendResult(PendingAck pendingAck, bool isSuccessful)
        {
            if (!isSuccessful)
            {
                ResponseStatus failureStatus = IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected;
                CompletePendingAck(pendingAck, failureStatus, CreateTerminalResponse(failureStatus), true);
                return;
            }

            IOutgoingMessage messageToRestore = null;

            lock (pendingAcksSync)
            {
                if (pendingAcks.TryGetValue(pendingAck.Id, out PendingAck registeredAck) &&
                    ReferenceEquals(registeredAck, pendingAck))
                {
                    pendingAck.SendState = PendingAckSendState.Sent;
                    messageToRestore = pendingAck.Message;
                    pendingAck.Message = null;
                }
            }

            if (messageToRestore != null)
                RestoreAckRequestId(messageToRestore, pendingAck);
        }

        private void CompletePendingAck(int ackId, ResponseStatus statusCode, IIncomingMessage message,
            bool isolateCallbackException)
        {
            PendingAck pendingAck = null;

            lock (pendingAcksSync)
            {
                if (pendingAcks.TryGetValue(ackId, out pendingAck))
                    pendingAcks.Remove(ackId);
            }

            if (pendingAck != null)
            {
                RestorePendingAckMessage(pendingAck);
                InvokeResponseCallback(pendingAck.Callback, statusCode, message, isolateCallbackException);
            }
        }

        private void CompletePendingAck(PendingAck pendingAck, ResponseStatus statusCode, IIncomingMessage message,
            bool isolateCallbackException)
        {
            bool wasRemoved = false;

            lock (pendingAcksSync)
            {
                if (pendingAcks.TryGetValue(pendingAck.Id, out PendingAck registeredAck) &&
                    ReferenceEquals(registeredAck, pendingAck))
                {
                    pendingAcks.Remove(pendingAck.Id);
                    wasRemoved = true;
                }
            }

            if (wasRemoved)
            {
                RestorePendingAckMessage(pendingAck);
                InvokeResponseCallback(pendingAck.Callback, statusCode, message, isolateCallbackException);
            }
        }

        private List<PendingAck> StopAcceptingAndDrainPendingAcks()
        {
            lock (pendingAcksSync)
            {
                acceptsAcks = false;

                if (pendingAcks.Count == 0)
                    return null;

                var drainedAcks = new List<PendingAck>(pendingAcks.Values);
                pendingAcks.Clear();
                return drainedAcks;
            }
        }

        private void CompletePendingAcks(List<PendingAck> completedAcks, ResponseStatus statusCode)
        {
            if (completedAcks == null)
                return;

            foreach (PendingAck pendingAck in completedAcks)
            {
                RestorePendingAckMessage(pendingAck);
                InvokeResponseCallback(pendingAck.Callback, statusCode, CreateTerminalResponse(statusCode), true);
            }
        }

        private void RestorePendingAckMessage(PendingAck pendingAck)
        {
            IOutgoingMessage message = pendingAck.Message;
            pendingAck.Message = null;

            if (message != null)
                RestoreAckRequestId(message, pendingAck);
        }

        private IIncomingMessage CreateTerminalResponse(ResponseStatus statusCode)
        {
            string message = statusCode switch
            {
                ResponseStatus.Timeout => "Time out",
                ResponseStatus.NotConnected => "Not connected",
                _ => "Failed to send request"
            };

            return new IncomingMessage("-1".ToUint16Hash(), 0, message.ToBytes(),
                DeliveryMethod.ReliableFragmentedSequenced, this)
            {
                Status = statusCode
            };
        }

        private void InvokeResponseCallback(ResponseCallback responseCallback, ResponseStatus statusCode,
            IIncomingMessage message, bool isolateException)
        {
            if (!isolateException)
            {
                responseCallback.Invoke(statusCode, message);
                return;
            }

            try
            {
                responseCallback.Invoke(statusCode, message);
            }
            catch (Exception exception)
            {
                TryLogError($"Response callback failed: {exception}");
            }
        }

        private void TryLogError(object message)
        {
            try
            {
                logger.Error(message);
            }
            catch
            {
                // Logging must not break acknowledgement cleanup or callback isolation.
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        public void HandleReceivedData(byte[] buffer)
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
            try
            {
                // Deserialize message from bytes
                IIncomingMessage message = MessageHelper.FromBytes(buffer, start, this);

                if (message == null)
                    return;

                if (message != null && message.AckRequestId.HasValue)
                {
                    // We received a message which is a response to our ack request
                    TriggerAck(message.AckRequestId.Value, message.Status, message);
                    return;
                }

                Mst.Traffic.RegisterOpCodeTrafic(message.OpCode, message.Data.LongLength, TrafficType.Incoming);
                HandleMessage(message);
            }
            catch (Exception ex)
            {
                Logs.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public virtual void HandleMessage(IIncomingMessage message)
        {
            NotifyMessageEvent(message);
        }

        #region Ack Disposal Stuff

        /// <summary>
        /// Called when ack disposal thread ticks
        /// </summary>
        private void HandleAckDisposalTick(long currentTick)
        {
            List<PendingAck> expiredAcks = null;

            lock (pendingAcksSync)
            {
                foreach (PendingAck pendingAck in pendingAcks.Values)
                {
                    if (pendingAck.DeadlineTick > currentTick)
                        continue;

                    expiredAcks ??= new List<PendingAck>();
                    expiredAcks.Add(pendingAck);
                }

                if (expiredAcks != null)
                {
                    foreach (PendingAck pendingAck in expiredAcks)
                    {
                        pendingAcks.Remove(pendingAck.Id);
                    }
                }
            }

            CompletePendingAcks(expiredAcks, ResponseStatus.Timeout);
        }

        #endregion

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            List<PendingAck> pendingAcksToComplete;

            lock (pendingAcksSync)
            {
                if (disposedValue)
                    return;

                disposedValue = true;
                acceptsAcks = false;

                pendingAcksToComplete = pendingAcks.Count > 0
                    ? new List<PendingAck>(pendingAcks.Values)
                    : null;
                pendingAcks.Clear();
            }

            if (disposing)
                MstTimer.OnTickEvent -= HandleAckDisposalTick;

            CompletePendingAcks(pendingAcksToComplete, ResponseStatus.NotConnected);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
