using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    public class WsClientSocket : BaseClientSocket, IClientSocket, IConnectionPermissionCredentials, IUpdatable
    {
        private sealed class PendingConnectionWait
        {
            public PendingConnectionWait(ConnectionDelegate callback)
            {
                Callback = callback;
            }

            public ConnectionDelegate Callback { get; }
            public Coroutine TimeoutCoroutine { get; set; }
            public bool IsCompleted { get; set; }
        }

        private const ushort NormalClosureCode = 1000;
        // Browser WebSockets allow application-defined close codes in the 4000-4999 range.
        private const ushort ClientUpdateFailureCloseCode = 4000;
        private const string ClientUpdateFailureCloseReason = "Client socket update failed";

        private readonly MasterServerToolkit.Logging.Logger logger;
        private MasterServerToolkit.Logging.LogLevel logLevel = MasterServerToolkit.Logging.LogLevel.Info;

        private string id = string.Empty;
        private WsClientPeer _peer;
        private WebSocket webSocket;
        private ConnectionStatus connectionStatus;
        private float connectionTimeout = 10f;

        // Lifecycle callbacks may request another connect or close. Defer those commands until every
        // observer of the current transition has seen the same socket state.
        private readonly Queue<Action> deferredConnectionLifecycleActions = new Queue<Action>();
        private readonly Dictionary<ushort, IPacketHandler> handlers = new Dictionary<ushort, IPacketHandler>();
        private readonly List<PendingConnectionWait> pendingConnectionWaits = new List<PendingConnectionWait>();
        private int connectionLifecycleDepth;
        private int connectionNotificationDepth;
        private bool isProcessingDeferredConnectionLifecycleActions;
        private bool wasTransportConnected;

        public bool IsConnected { get; private set; } = false;
        public bool IsConnecting => connectionStatus == ConnectionStatus.Connecting ||
                                    connectionStatus == ConnectionStatus.Authenticating;
        public string Address { get; private set; }
        public int Port { get; private set; }
        public ConnectionStatus Status
        {
            get
            {
                return connectionStatus;
            }
            set
            {
                if (connectionStatus != value)
                {
                    connectionStatus = value;
                    InvokeConnectionNotification(() => InvokeStatusChangedEvent(connectionStatus));
                }
            }
        }
        public bool UseSecure { get; set; }
        public string Service { get; set; } = "mst";
        public string PermissionKey { get; set; } = string.Empty;
        public string PermissionCredential { get; set; } = string.Empty;
        public ushort CloseCode { get; private set; }

        public int Priority => -100;

        public MasterServerToolkit.Logging.LogLevel LogLevel
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

        public string Id => id;

        public event ConnectionDelegate OnConnectionOpenEvent;
        public event ConnectionDelegate OnConnectionCloseEvent;
        public event ConnectionStatusDelegate OnStatusChangedEvent;

        public WsClientSocket()
        {
            id = Guid.NewGuid().ToString();
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            connectionStatus = ConnectionStatus.Disconnected;
        }

        private void OnMessageReceivedHandler(IIncomingMessage message)
        {
            try
            {
                if (handlers.TryGetValue(message.OpCode, out IPacketHandler handler))
                {
                    if (handler != null)
                    {
                        handler.Handle(message);
                    }
                    else
                    {
                        Logs.Error($"Connection is missing a handler. OpCode: {Extensions.StringExtensions.FromHash(message.OpCode)}");
                    }
                }
                else if (message.IsExpectingResponse)
                {
                    Logs.Error($"Connection is missing a handler. OpCode: {Extensions.StringExtensions.FromHash(message.OpCode)}");
                    message.Respond(ResponseStatus.Error);
                }
            }
            catch (Exception e)
            {
                Logs.Error($"Failed to handle a message. OpCode: {Extensions.StringExtensions.FromHash(message.OpCode)}, Error: {e}");

                if (!message.IsExpectingResponse)
                {
                    return;
                }

                try
                {
                    message.Respond(ResponseStatus.Error);
                }
                catch (Exception exception)
                {
                    Logs.Error(exception);
                }
            }
        }

        public void WaitForConnection(ConnectionDelegate connectionCallback, float timeoutSeconds)
        {
            if (connectionCallback == null)
                throw new ArgumentNullException(nameof(connectionCallback));

            if (IsConnected)
            {
                InvokeConnectionNotification(() => InvokeConnectionCallback(connectionCallback, "Connection wait callback"));
                return;
            }

            if (!IsConnecting)
            {
                InvokeConnectionNotification(() => InvokeConnectionCallback(connectionCallback, "Connection wait callback"));
                return;
            }

            var pendingWait = new PendingConnectionWait(connectionCallback);
            pendingConnectionWaits.Add(pendingWait);

            Coroutine timeoutCoroutine;

            try
            {
                timeoutCoroutine = MstTimer.WaitForRealtimeSeconds(timeoutSeconds, () =>
                {
                    pendingWait.TimeoutCoroutine = null;
                    CompleteConnectionWait(pendingWait);
                });
            }
            catch (Exception exception)
            {
                TryLogError($"Failed to start connection wait timeout: {exception}");
                CompleteConnectionWait(pendingWait);
                return;
            }

            if (pendingWait.IsCompleted)
            {
                MstTimer.TryStopCoroutine(timeoutCoroutine);
                return;
            }

            pendingWait.TimeoutCoroutine = timeoutCoroutine;

            if (timeoutCoroutine == null)
                CompleteConnectionWait(pendingWait);
        }

        private void CompleteConnectionWait(PendingConnectionWait pendingWait)
        {
            if (pendingWait == null || pendingWait.IsCompleted)
                return;

            pendingWait.IsCompleted = true;
            pendingConnectionWaits.Remove(pendingWait);

            Coroutine timeoutCoroutine = pendingWait.TimeoutCoroutine;
            pendingWait.TimeoutCoroutine = null;
            MstTimer.TryStopCoroutine(timeoutCoroutine);

            InvokeConnectionNotification(() => InvokeConnectionWaitCallback(pendingWait));
        }

        private List<PendingConnectionWait> DrainPendingConnectionWaits()
        {
            if (pendingConnectionWaits.Count == 0)
                return null;

            var completedWaits = new List<PendingConnectionWait>(pendingConnectionWaits);
            pendingConnectionWaits.Clear();

            foreach (PendingConnectionWait pendingWait in completedWaits)
            {
                pendingWait.IsCompleted = true;

                Coroutine timeoutCoroutine = pendingWait.TimeoutCoroutine;
                pendingWait.TimeoutCoroutine = null;
                MstTimer.TryStopCoroutine(timeoutCoroutine);
            }

            return completedWaits;
        }

        private void InvokeConnectionWaitCallbacks(List<PendingConnectionWait> completedWaits)
        {
            if (completedWaits == null)
                return;

            foreach (PendingConnectionWait completedWait in completedWaits)
            {
                InvokeConnectionWaitCallback(completedWait);
            }
        }

        private void InvokeConnectionWaitCallback(PendingConnectionWait completedWait)
        {
            InvokeConnectionCallback(completedWait.Callback, "Connection wait callback");
        }

        private void InvokeConnectionCallback(ConnectionDelegate callback, string callbackType)
        {
            try
            {
                callback.Invoke(this);
            }
            catch (Exception exception)
            {
                TryLogError($"{callbackType} failed: {exception}");
            }
        }

        private void InvokeConnectionTransition(ConnectionDelegate connectionEvent, List<PendingConnectionWait> completedWaits)
        {
            InvokeConnectionNotification(() =>
            {
                InvokeConnectionEvent(connectionEvent);
                InvokeConnectionWaitCallbacks(completedWaits);
            });
        }

        private void InvokeConnectionEvent(ConnectionDelegate connectionEvent)
        {
            if (connectionEvent == null)
                return;

            foreach (ConnectionDelegate callback in connectionEvent.GetInvocationList())
            {
                InvokeConnectionCallback(callback, "Connection event callback");
            }
        }

        private void InvokeStatusChangedEvent(ConnectionStatus status)
        {
            ConnectionStatusDelegate statusChangedEvent = OnStatusChangedEvent;

            if (statusChangedEvent == null)
                return;

            foreach (ConnectionStatusDelegate callback in statusChangedEvent.GetInvocationList())
            {
                try
                {
                    callback.Invoke(status);
                }
                catch (Exception exception)
                {
                    TryLogError($"Connection status callback failed: {exception}");
                }
            }
        }

        private void InvokeConnectionNotification(Action notification)
        {
            connectionNotificationDepth++;

            try
            {
                notification.Invoke();
            }
            finally
            {
                connectionNotificationDepth--;
                ProcessDeferredConnectionLifecycleActions();
            }
        }

        private void ExecuteConnectionLifecycleAction(Action lifecycleAction)
        {
            if (connectionNotificationDepth > 0 || connectionLifecycleDepth > 0)
            {
                deferredConnectionLifecycleActions.Enqueue(lifecycleAction);
                return;
            }

            connectionLifecycleDepth++;

            try
            {
                lifecycleAction.Invoke();
            }
            finally
            {
                connectionLifecycleDepth--;
                ProcessDeferredConnectionLifecycleActions();
            }
        }

        private void ProcessDeferredConnectionLifecycleActions()
        {
            if (connectionLifecycleDepth > 0 ||
                connectionNotificationDepth > 0 ||
                isProcessingDeferredConnectionLifecycleActions)
                return;

            isProcessingDeferredConnectionLifecycleActions = true;

            try
            {
                while (deferredConnectionLifecycleActions.Count > 0)
                {
                    Action lifecycleAction = deferredConnectionLifecycleActions.Dequeue();

                    try
                    {
                        ExecuteConnectionLifecycleAction(lifecycleAction);
                    }
                    catch (Exception exception)
                    {
                        TryLogError($"Deferred connection lifecycle action failed: {exception}");
                    }
                }
            }
            finally
            {
                isProcessingDeferredConnectionLifecycleActions = false;
            }
        }

        public void WaitForConnection(ConnectionDelegate connectionCallback)
        {
            WaitForConnection(connectionCallback, connectionTimeout);
        }

        public void AddConnectionOpenListener(ConnectionDelegate callback, bool invokeInstantlyIfConnected = true)
        {
            // Remove copy of the callback method to prevent double invocation
            RemoveConnectionOpenListener(callback);

            // Asign callback method again
            OnConnectionOpenEvent += callback;

            if (IsConnected && invokeInstantlyIfConnected)
            {
                InvokeConnectionNotification(() => InvokeConnectionEvent(callback));
            }
        }

        public void RemoveConnectionOpenListener(ConnectionDelegate callback)
        {
            OnConnectionOpenEvent -= callback;
        }

        public void AddConnectionCloseListener(ConnectionDelegate callback, bool invokeInstantlyIfDisconnected = true)
        {
            // Remove copy of the callback method to prevent double invocation
            RemoveConnectionCloseListener(callback);

            // Asign callback method again
            OnConnectionCloseEvent += callback;

            if (Status == ConnectionStatus.Disconnected && invokeInstantlyIfDisconnected)
            {
                InvokeConnectionNotification(() => InvokeConnectionEvent(callback));
            }
        }

        public void RemoveConnectionCloseListener(ConnectionDelegate callback)
        {
            OnConnectionCloseEvent -= callback;
        }

        public IPacketHandler RegisterMessageHandler(IPacketHandler handler)
        {
            if (handlers.ContainsKey(handler.OpCode))
            {
                Logs.Warn($"The handler with code [{Extensions.StringExtensions.FromHash(handler.OpCode)}] has already been registered. " +
                    $"Overwriting it with new one");
            }

            handlers[handler.OpCode] = handler;
            return handler;
        }

        public IPacketHandler RegisterMessageHandler(ushort opCode, IncommingMessageHandler handlerMethod)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            RegisterMessageHandler(handler);
            return handler;
        }

        public void UnregisterMessageHandler(IPacketHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            if (handlers.TryGetValue(handler.OpCode, out IPacketHandler registeredHandler) && ReferenceEquals(registeredHandler, handler))
            {
                handlers.Remove(handler.OpCode);
            }
        }

        public void UnregisterMessageHandler(ushort opCode)
        {
            handlers.Remove(opCode);
        }

        public void Reconnect(bool fireEvent = true)
        {
            string address = Address;
            int port = Port;
            float timeoutSeconds = connectionTimeout;

            ExecuteConnectionLifecycleAction(() =>
            {
                CloseInternal(NormalClosureCode, "Closed by client", fireEvent);
                StartConnectionInternal(address, port, timeoutSeconds);
            });
        }

        public void DoUpdate()
        {
            WebSocket updatingSocket = webSocket;
            WsClientPeer updatingPeer = _peer;

            try
            {
                ProcessUpdate(updatingSocket, updatingPeer);
            }
            catch (Exception exception)
            {
                HandleUpdateFailure(updatingSocket, updatingPeer, exception);
            }
        }

        private void ProcessUpdate(WebSocket currentSocket, WsClientPeer currentPeer)
        {
            if (currentSocket == null)
            {
                return;
            }

            bool isTransportConnected = currentSocket.IsConnected;
            bool transportWasClosed = wasTransportConnected && !isTransportConnected;
            bool connectionAttemptStopped = Status == ConnectionStatus.Connecting &&
                !currentSocket.IsConnecting && !isTransportConnected;

            if (transportWasClosed || connectionAttemptStopped)
            {
                DrainReceivedMessages(currentSocket, currentPeer);

                if (ReferenceEquals(webSocket, currentSocket) && ReferenceEquals(_peer, currentPeer))
                    SetStatus(ConnectionStatus.Disconnected);

                return;
            }

            currentPeer?.ProcessSendCompletions();

            if (!ReferenceEquals(webSocket, currentSocket) || !ReferenceEquals(_peer, currentPeer))
                return;

            // Start MST authentication before dispatching data already queued by WebSocket OnMessage.
            if (!wasTransportConnected && isTransportConnected)
            {
                wasTransportConnected = true;
                SetStatus(ConnectionStatus.Authenticating);

                if (!ReferenceEquals(webSocket, currentSocket))
                    return;
            }

            if (!DrainReceivedMessages(currentSocket, _peer))
                return;

            isTransportConnected = currentSocket.IsConnected;

            // Check if status changed
            if (wasTransportConnected != isTransportConnected)
            {
                wasTransportConnected = isTransportConnected;
                SetStatus(isTransportConnected ? ConnectionStatus.Authenticating : ConnectionStatus.Disconnected);
            }
            else if (Status == ConnectionStatus.Connecting && !webSocket.IsConnecting && !isTransportConnected)
            {
                SetStatus(ConnectionStatus.Disconnected);
            }
        }

        private void HandleUpdateFailure(WebSocket failedSocket, WsClientPeer failedPeer, Exception exception)
        {
            TryLogError($"WebSocket client update failed. Address: {Address}:{Port}, status: {Status}. Error: {exception}");

            if (!ReferenceEquals(webSocket, failedSocket) || !ReferenceEquals(_peer, failedPeer))
                return;

            try
            {
                ExecuteConnectionLifecycleAction(() =>
                {
                    if (ReferenceEquals(webSocket, failedSocket) && ReferenceEquals(_peer, failedPeer))
                        CloseInternal(ClientUpdateFailureCloseCode, ClientUpdateFailureCloseReason, true);
                });
            }
            catch (Exception cleanupException)
            {
                try
                {
                    MstUpdateRunner.Remove(this);
                }
                catch
                {
                    // Preserve the original update failure even if runner cleanup is unavailable.
                }

                TryLogError($"WebSocket client cleanup failed after an update error: {cleanupException}");
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
                // Logging must not prevent transport cleanup.
            }
        }

        private bool DrainReceivedMessages(WebSocket currentSocket, WsClientPeer currentPeer)
        {
            byte[] data = currentSocket.Recv();

            while (data != null)
            {
                currentPeer?.HandleReceivedData(data);

                if (!ReferenceEquals(webSocket, currentSocket) || !ReferenceEquals(_peer, currentPeer))
                    return false;

                data = currentSocket.Recv();
            }

            return true;
        }

        private void SetStatus(ConnectionStatus status, bool fireEvent = true)
        {
            ExecuteConnectionLifecycleAction(() => SetStatusInternal(status, fireEvent));
        }

        private void SetStatusInternal(ConnectionStatus status, bool fireEvent)
        {
            switch (status)
            {
                case ConnectionStatus.Connecting:

                    IsConnected = false;

                    if (Status != ConnectionStatus.Connecting)
                        Status = ConnectionStatus.Connecting;

                    break;
                case ConnectionStatus.Authenticating:

                    if (Status != ConnectionStatus.Authenticating)
                    {
                        Status = ConnectionStatus.Authenticating;
                        WebSocket authenticatingSocket = webSocket;
                        WsClientPeer authenticatingPeer = _peer;

                        Mst.Security.AuthenticateConnection(this, (isSuccess, error) =>
                        {
                            ExecuteConnectionLifecycleAction(() =>
                            {
                                if (!ReferenceEquals(webSocket, authenticatingSocket) ||
                                    !ReferenceEquals(_peer, authenticatingPeer) ||
                                    !authenticatingSocket.IsConnected)
                                    return;

                                if (!isSuccess)
                                {
                                    CloseInternal(1008, string.IsNullOrEmpty(error) ? "Access denied" : error, fireEvent);
                                    return;
                                }

                                IsConnected = true;
                                Status = ConnectionStatus.Connected;

                                ConnectionDelegate connectionOpenEvent = fireEvent ? OnConnectionOpenEvent : null;
                                InvokeConnectionTransition(connectionOpenEvent, DrainPendingConnectionWaits());
                            });
                        });
                    }

                    break;
                case ConnectionStatus.Disconnected:
                    ushort closeCode = (ushort)(webSocket != null ? webSocket.CloseCode : CloseCode);
                    CloseInternal(closeCode, "Transport disconnected", fireEvent, false);
                    break;
            }
        }

        public IClientSocket Connect(string ip, int port)
        {
            return Connect(ip, port, connectionTimeout);
        }

        public IClientSocket Connect(string ip, int port, float timeoutSeconds)
        {
            ExecuteConnectionLifecycleAction(() =>
            {
                CloseInternal(NormalClosureCode, "Closed by client", false);
                StartConnectionInternal(ip, port, timeoutSeconds);
            });

            return this;
        }

        private void StartConnectionInternal(string ip, int port, float timeoutSeconds)
        {
            try
            {
                connectionTimeout = timeoutSeconds;

                Address = ip;
                Port = port;

                Status = ConnectionStatus.Connecting;

                if (UseSecure)
                {
                    webSocket = new WebSocket(new Uri($"wss://{ip}:{port}/{Service}"));
                }
                else
                {
                    webSocket = new WebSocket(new Uri($"ws://{ip}:{port}/{Service}"));
                }

                _peer = new WsClientPeer(webSocket);
                _peer.OnMessageReceivedEvent += OnMessageReceivedHandler;

                Peer = _peer;

                MstUpdateRunner.Add(this);

                _peer.Connect();
            }
            catch
            {
                CloseInternal(NormalClosureCode, "Connection setup failed", true);
                throw;
            }
        }

        public void Close(bool fireEvent = true)
        {
            Close(NormalClosureCode, fireEvent);
        }

        public void Close(ushort code, bool fireEvent = true)
        {
            Close(code, "Closed by client", fireEvent);
        }

        public void Close(ushort code, string reason, bool fireEvent = true)
        {
            ExecuteConnectionLifecycleAction(() => CloseInternal(code, reason, fireEvent));
        }

        private void CloseInternal(ushort code, string reason, bool fireEvent, bool closeTransport = true)
        {
            MstUpdateRunner.Remove(this);

            var socketToClose = webSocket;
            var peerToDispose = _peer;
            bool shouldNotifyClose = Status != ConnectionStatus.Disconnected;
            webSocket = null;
            _peer = null;

            IsConnected = false;
            wasTransportConnected = false;
            CloseCode = code;

            if (shouldNotifyClose)
                Status = ConnectionStatus.Disconnected;

            if (closeTransport)
            {
                try
                {
                    socketToClose?.Close(code, reason);
                }
                catch (Exception exception)
                {
                    TryLogError($"Failed to close WebSocket transport: {exception}");
                }
            }

            try
            {
                socketToClose?.Dispose();
            }
            catch (Exception exception)
            {
                TryLogError($"Failed to release WebSocket transport: {exception}");
            }

            if (peerToDispose != null)
            {
                try
                {
                    peerToDispose.OnMessageReceivedEvent -= OnMessageReceivedHandler;
                    peerToDispose.Dispose();
                }
                catch (Exception exception)
                {
                    TryLogError($"Failed to dispose WebSocket peer: {exception}");
                }
            }

            ConnectionDelegate connectionCloseEvent = shouldNotifyClose && fireEvent ? OnConnectionCloseEvent : null;
            InvokeConnectionTransition(connectionCloseEvent, DrainPendingConnectionWaits());
        }
    }
}
