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
        public ushort CloseCode { get; private set; }

        public event ConnectionDelegate OnConnectionOpenEvent;
        public event ConnectionDelegate OnConnectionCloseEvent;
        public event ConnectionStatusDelegate OnStatusChangedEvent;

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
                        _peer.SendDelayedMessages();

                        // Client should be validated
                        Mst.Security.AuthenticateConnection(this, (isSuccess, error) =>
                        {
                            Status = ConnectionStatus.Connected;

                            if (isSuccess && fireEvent)
                                OnConnectionOpenEvent?.Invoke(this);
                        });
                    }

                    break;
                case ConnectionStatus.Disconnected:

                    if (Status != ConnectionStatus.Disconnected)
                    {
                        Status = ConnectionStatus.Disconnected;

                        CloseCode = (ushort)(webSocket != null ? webSocket.CloseCode : 0);

                        if (fireEvent)
                            OnConnectionCloseEvent?.Invoke(this);
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

        public void WaitForConnection(ConnectionDelegate connectionCallback, float timeoutSeconds)
        {
            if (IsConnected)
            {
                connectionCallback.Invoke(this);
                return;
            }

            var isConnected = false;
            var timedOut = false;

            // Make local function
            void onConnected(IClientSocket client)
            {
                OnConnectionOpenEvent -= onConnected;
                isConnected = true;

                if (!timedOut)
                {
                    connectionCallback.Invoke(client);
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
                callback.Invoke(this);
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

            if (!IsConnected && invokeInstantlyIfDisconnected)
            {
                callback.Invoke(this);
            }
        }

        public void RemoveConnectionCloseListener(ConnectionDelegate callback)
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
                _peer.HandleReceivedData(data);
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
                webSocket = new WebSocket(new Uri($"wss://{ip}:{port}/app/{Mst.Settings.ApplicationKey}"));
            }
            else
            {
                webSocket = new WebSocket(new Uri($"ws://{ip}:{port}/app/{Mst.Settings.ApplicationKey}"));
            }

            _peer = new WsClientPeer(webSocket);
            _peer.OnMessageReceivedEvent += HandleMessage;

            Peer = _peer;

            MstUpdateRunner.Add(this);

#if UNITY_WEBGL && !UNITY_EDITOR
            MstUpdateRunner.Instance.StartCoroutine(webSocket.Connect());
#else
            webSocket.Connect();
#endif
            return this;
        }

        public void Close(bool fireEvent = true)
        {
            Close((ushort)CloseStatusCode.Normal, fireEvent);
        }

        public void Close(ushort code, bool fireEvent = true)
        {
            Close(code, "", fireEvent);
        }

        public void Close(ushort code, string reason, bool fireEvent = true)
        {
            //MstUpdateRunner.Remove(this);

            if (webSocket != null)
                webSocket.Close(code, reason);

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