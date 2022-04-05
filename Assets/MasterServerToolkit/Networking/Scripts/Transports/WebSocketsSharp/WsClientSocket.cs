using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using WebSocketSharp;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Client for connecting to websocket server.
    /// </summary>
    public class WsClientSocket : BaseClientSocket, IClientSocket, IUpdatable
    {
        private WsClientPeer _peer;
        private WebSocket webSocket;
        private ConnectionStatus status;
        private readonly Dictionary<ushort, IPacketHandler> handlers;
        private float connectionTimeout = 10f;
        private bool wasConnected = false;

        public bool IsConnected { get; private set; } = false;
        public bool IsConnecting { get { return status == ConnectionStatus.Connecting; } }
        public string ConnectionIp { get; private set; }
        public int ConnectionPort { get; private set; }
        public ConnectionStatus Status
        {
            get
            {
                return status;
            }
            set
            {
                if (status != value)
                {
                    status = value;
                    OnStatusChangedEvent?.Invoke(status);
                }
            }
        }
        public bool UseSecure { get; set; }

        public event Action OnConnectionOpenEvent;
        public event Action OnConnectionCloseEvent;
        public event Action<ConnectionStatus> OnStatusChangedEvent;

        public WsClientSocket()
        {
            SetStatus(ConnectionStatus.Disconnected, false);
            handlers = new Dictionary<ushort, IPacketHandler>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status"></param>
        private void SetStatus(ConnectionStatus status, bool fireEvent = true)
        {
            switch (status)
            {
                case ConnectionStatus.Connecting:

                    if (Status != ConnectionStatus.Connecting)
                        Status = ConnectionStatus.Connecting;

                    break;
                case ConnectionStatus.Connected:

                    if (Status != ConnectionStatus.Connected)
                    {
                        Status = ConnectionStatus.Connected;
                        _peer.SendDelayedMessages();

                        if (fireEvent)
                            OnConnectionOpenEvent?.Invoke();
                    }

                    break;
                case ConnectionStatus.Disconnected:

                    if (Status != ConnectionStatus.Disconnected)
                    {
                        Status = ConnectionStatus.Disconnected;

                        if (fireEvent)
                            OnConnectionCloseEvent?.Invoke();
                    }

                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        private void HandleMessage(IIncomingMessage message)
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
                        Logs.Error($"Connection is missing a handler. OpCode: {message.OpCode}");
                    }
                }
                else if (message.IsExpectingResponse)
                {
                    Logs.Error($"Connection is missing a handler. OpCode: {message.OpCode}");
                    message.Respond(ResponseStatus.Error);
                }
            }
            catch (Exception e)
            {
                Logs.Error($"Failed to handle a message. OpCode: {message.OpCode}, Error: {e}");

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

        public void WaitForConnection(Action<IClientSocket> connectionCallback, float timeoutSeconds)
        {
            if (IsConnected)
            {
                connectionCallback.Invoke(this);
                return;
            }

            var isConnected = false;
            var timedOut = false;

            // Make local function
            void onConnected()
            {
                OnConnectionOpenEvent -= onConnected;
                isConnected = true;

                if (!timedOut)
                {
                    connectionCallback.Invoke(this);
                }
            }

            // Listen to connection event
            OnConnectionOpenEvent += onConnected;

            // Wait for some seconds
            MstTimer.WaitForSeconds(timeoutSeconds, () =>
            {
                if (!isConnected)
                {
                    timedOut = true;
                    OnConnectionOpenEvent -= onConnected;
                    connectionCallback.Invoke(this);
                }
            });
        }

        public void WaitForConnection(Action<IClientSocket> connectionCallback)
        {
            WaitForConnection(connectionCallback, connectionTimeout);
        }

        public void AddConnectionOpenListener(Action callback, bool invokeInstantlyIfConnected = true)
        {
            // Remove copy of the callback method to prevent double invocation
            RemoveConnectionOpenListener(callback);

            // Asign callback method again
            OnConnectionOpenEvent += callback;

            if (IsConnected && invokeInstantlyIfConnected)
            {
                callback.Invoke();
            }
        }

        public void RemoveConnectionOpenListener(Action callback)
        {
            OnConnectionOpenEvent -= callback;
        }

        public void AddConnectionCloseListener(Action callback, bool invokeInstantlyIfDisconnected = true)
        {
            // Remove copy of the callback method to prevent double invocation
            RemoveConnectionCloseListener(callback);

            // Asign callback method again
            OnConnectionCloseEvent += callback;

            if (!IsConnected && invokeInstantlyIfDisconnected)
            {
                callback.Invoke();
            }
        }

        public void RemoveConnectionCloseListener(Action callback)
        {
            OnConnectionCloseEvent -= callback;
        }

        public IPacketHandler RegisterMessageHandler(IPacketHandler handler)
        {
            handlers[handler.OpCode] = handler;
            return handler;
        }

        public IPacketHandler RegisterMessageHandler(ushort opCode, IncommingMessageHandler handlerMethod)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            RegisterMessageHandler(handler);
            return handler;
        }

        public void RemoveMessageHandler(IPacketHandler handler)
        {
            // But only if this exact handler
            if (handlers.TryGetValue(handler.OpCode, out IPacketHandler previousHandler) && previousHandler != handler)
            {
                return;
            }

            handlers.Remove(handler.OpCode);
        }

        public void Reconnect(bool fireEvent = true)
        {
            Close(fireEvent);
            Connect(ConnectionIp, ConnectionPort);
        }

        public void Update()
        {
            if (webSocket == null)
            {
                return;
            }

            // Get all received bytes
            byte[] data = webSocket.Recv();

            while (data != null)
            {
                _peer.HandleDataReceived(data);
                data = webSocket.Recv();
            }

            wasConnected = IsConnected;
            IsConnected = webSocket.IsConnected;

            // Check if status changed
            if (wasConnected != IsConnected)
            {
                SetStatus(IsConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected);
            }
        }

        public IClientSocket Connect(string ip, int port)
        {
            return Connect(ip, port, connectionTimeout);
        }

        public IClientSocket Connect(string ip, int port, float timeoutSeconds)
        {
            Close(false);

            connectionTimeout = timeoutSeconds;

            ConnectionIp = ip;
            ConnectionPort = port;

            SetStatus(ConnectionStatus.Connecting);

            if (UseSecure)
            {
                webSocket = new WebSocket(new Uri($"wss://{ip}:{port}/app/{MstApplicationConfig.Instance.ApplicationKey}"));
            }
            else
            {
                webSocket = new WebSocket(new Uri($"ws://{ip}:{port}/app/{MstApplicationConfig.Instance.ApplicationKey}"));
            }

            _peer = new WsClientPeer(webSocket);
            _peer.OnMessageReceivedEvent += HandleMessage;

            Peer = _peer;

            MstUpdateRunner.Instance.Add(this);

#if UNITY_WEBGL && !UNITY_EDITOR
            MstTimer.Singleton.StartCoroutine(webSocket.Connect());
#else
            webSocket.Connect();
#endif
            return this;
        }

        public void Close(bool fireEvent = true)
        {
            MstUpdateRunner.Instance.Remove(this);

            if (webSocket != null)
                webSocket.Close();

            if (_peer != null)
            {
                _peer.OnMessageReceivedEvent -= HandleMessage;
                _peer.Dispose();
            }

            IsConnected = false;
            SetStatus(ConnectionStatus.Disconnected, fireEvent);
        }
    }
}