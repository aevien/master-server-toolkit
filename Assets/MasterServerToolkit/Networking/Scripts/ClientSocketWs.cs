using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Client for connecting to websocket server.
    /// </summary>
    public class ClientSocketWs : BaseClientSocket<PeerWs>, IClientSocket, IUpdatable
    {
        IPeer IMsgDispatcher<IPeer>.Peer { get; }

        private WebSocket webSocket;
        private ConnectionStatus status;
        private readonly Dictionary<short, IPacketHandler> handlers;
        private float connectionTimeout = 10f;

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
        public bool UseSsl { get; set; }

        public event Action OnConnectedEvent;
        public event Action OnDisconnectedEvent;
        public event Action<ConnectionStatus> OnStatusChangedEvent;

        public ClientSocketWs()
        {
            SetStatus(ConnectionStatus.Disconnected);
            handlers = new Dictionary<short, IPacketHandler>();
        }

        private void SetStatus(ConnectionStatus status)
        {
            switch (status)
            {
                case ConnectionStatus.Connecting:

                    if (Status != ConnectionStatus.Connecting)
                    {
                        Status = ConnectionStatus.Connecting;
                    }

                    break;
                case ConnectionStatus.Connected:

                    if (Status != ConnectionStatus.Connected)
                    {
                        Status = ConnectionStatus.Connected;
                        MstTimer.Instance.StartCoroutine(Peer.SendDelayedMessages());
                        OnConnectedEvent?.Invoke();
                    }

                    break;
                case ConnectionStatus.Disconnected:

                    if (Status != ConnectionStatus.Disconnected)
                    {
                        Status = ConnectionStatus.Disconnected;
                        OnDisconnectedEvent?.Invoke();
                    }

                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        private void HandleMessage(IIncommingMessage message)
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
                OnConnectedEvent -= onConnected;
                isConnected = true;

                if (!timedOut)
                {
                    connectionCallback.Invoke(this);
                }
            }

            // Listen to connection event
            OnConnectedEvent += onConnected;

            // Wait for some seconds
            MstTimer.WaitForSeconds(timeoutSeconds, () =>
            {
                if (!isConnected)
                {
                    timedOut = true;
                    OnConnectedEvent -= onConnected;
                    connectionCallback.Invoke(this);
                }
            });
        }

        public void WaitForConnection(Action<IClientSocket> connectionCallback)
        {
            WaitForConnection(connectionCallback, connectionTimeout);
        }

        public void AddConnectionListener(Action callback, bool invokeInstantlyIfConnected = true)
        {
            // Asign callback method again
            OnConnectedEvent += callback;

            if (IsConnected && invokeInstantlyIfConnected)
            {
                callback.Invoke();
            }
        }

        public void RemoveConnectionListener(Action callback)
        {
            OnConnectedEvent -= callback;
        }

        public void AddDisconnectionListener(Action callback, bool invokeInstantlyIfDisconnected = true)
        {
            // Remove copy of the callback method to prevent double invocation
            OnDisconnectedEvent -= callback;

            // Asign callback method again
            OnDisconnectedEvent += callback;

            if (!IsConnected && invokeInstantlyIfDisconnected)
            {
                callback.Invoke();
            }
        }

        public void RemoveDisconnectionListener(Action callback)
        {
            OnDisconnectedEvent -= callback;
        }

        public IPacketHandler SetHandler(IPacketHandler handler)
        {
            handlers[handler.OpCode] = handler;
            return handler;
        }

        public IPacketHandler SetHandler(short opCode, IncommingMessageHandler handlerMethod)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            SetHandler(handler);
            return handler;
        }

        public void RemoveHandler(IPacketHandler handler)
        {
            // But only if this exact handler
            if (handlers.TryGetValue(handler.OpCode, out IPacketHandler previousHandler) && previousHandler != handler)
            {
                return;
            }

            handlers.Remove(handler.OpCode);
        }

        public void Reconnect()
        {
            Disconnect();
            Connect(ConnectionIp, ConnectionPort);
        }

        public void Update()
        {
            if (webSocket == null)
            {
                return;
            }

            byte[] data = webSocket.Recv();

            while (data != null)
            {
                Peer.HandleDataReceived(data, 0);
                data = webSocket.Recv();
            }

            bool wasConnected = IsConnected;
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
            connectionTimeout = timeoutSeconds;

            ConnectionIp = ip;
            ConnectionPort = port;

            if (webSocket != null && webSocket.IsConnected)
            {
                webSocket.Close();
            }

            IsConnected = false;
            SetStatus(ConnectionStatus.Connecting);

            if (Peer != null)
            {
                Peer.OnMessageReceivedEvent -= HandleMessage;
                Peer.Dispose();
            }

            if (UseSsl)
            {
                webSocket = new WebSocket(new Uri($"wss://{ip}:{port}/msf"));
            }
            else
            {
                webSocket = new WebSocket(new Uri($"ws://{ip}:{port}/msf"));
            }

            Peer = new PeerWs(webSocket);
            Peer.OnMessageReceivedEvent += HandleMessage;

            MstUpdateRunner.Instance.Add(this);
            MstUpdateRunner.Instance.StartCoroutine(webSocket.Connect());

            return this;
        }

        public void Disconnect()
        {
            if (webSocket != null)
            {
                webSocket.Close();
            }

            if (Peer != null)
            {
                Peer.Dispose();
            }

            IsConnected = false;
            SetStatus(ConnectionStatus.Disconnected);
        }
    }
}