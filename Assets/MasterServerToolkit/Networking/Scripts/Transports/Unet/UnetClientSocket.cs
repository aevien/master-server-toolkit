using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace MasterServerToolkit.Networking.Unet
{
    /// <summary>
    /// Represents a socket (client socket), which can be used to connect
    /// to another socket (server socket)
    /// </summary>
    public class UnetClientSocket : BaseClientSocket, IClientSocket, IUpdatable
    {
        public static bool RethrowExceptionsInEditor = true;

        private readonly HostTopology _topology;
        private int _connectionId;

        private readonly Dictionary<short, IPacketHandler> _handlers;

        private string _ip;
        private int _port;

        private bool _isConnectionPending;
        private byte[] _msgBuffer;

        private PeerUnet _peer;
        public static int _socketId;

        private ConnectionStatus status;
        private int _stopConnectingTick;

        /// <summary>
        /// Event, which is invoked when we successfully 
        /// connect to another socket
        /// </summary>
        public event Action OnConnectedEvent;

        /// <summary>
        /// Event, which is invoked when we are
        /// disconnected from another socket
        /// </summary>
        public event Action OnDisconnectedEvent;

        /// <summary>
        /// Event, invoked when connection status changes
        /// </summary>
        public event Action<ConnectionStatus> OnStatusChangedEvent;

        /// <summary>
        /// Returns true, if we are connected to another socket
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Returns true, if we're in the process of connecting
        /// </summary>
        public bool IsConnecting { get; private set; }

        /// <summary>
        /// Connection status
        /// </summary>
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

        public string ConnectionIp { get; private set; }

        public int ConnectionPort { get; private set; }
        public bool UseSecure { get; set; } = false;

        public UnetClientSocket() : this(UnetSocketTopology.Topology)
        {
            _handlers = new Dictionary<short, IPacketHandler>();
        }

        public UnetClientSocket(HostTopology topology)
        {
            _msgBuffer = new byte[0xFFFF];
            _topology = topology;
        }

        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public IClientSocket Connect(string ip, int port)
        {
            Connect(ip, port, 10000);
            return this;
        }

        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeoutMillis"></param>
        /// <returns></returns>
        public IClientSocket Connect(string ip, int port, float timeoutMillis)
        {
            ConnectionIp = ip;
            ConnectionPort = port;
            NetworkTransport.Init();
            _stopConnectingTick = Environment.TickCount + (int)timeoutMillis;
            _ip = ip;
            _port = port;

            IsConnecting = true;

            _socketId = NetworkTransport.AddHost(_topology, 0);

            Logs.Info(_socketId);

            MstUpdateRunner.Singleton.Add(this);

            return this;
        }

        /// <summary>
        /// Disconnects and connects again
        /// </summary>
        public void Reconnect()
        {
            Disconnect();
            Connect(_ip, _port);
        }

        /// <summary>
        /// Adds a listener, which is invoked when connection is established,
        /// or instantly, if already connected and  <see cref="invokeInstantlyIfConnected"/> 
        /// is true
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="invokeInstantlyIfConnected"></param>
        public void AddConnectionListener(Action callback, bool invokeInstantlyIfConnected = true)
        {
            OnConnectedEvent += callback;

            if (IsConnected && invokeInstantlyIfConnected)
                callback.Invoke();
        }

        /// <summary>
        /// Removes connection listener
        /// </summary>
        /// <param name="callback"></param>
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

        /// <summary>
        /// Invokes a callback after a successful connection,
        /// instantly if connected, or after the timeout, if failed to connect
        /// </summary>
        /// <param name="connectionCallback"></param>
        public void WaitForConnection(Action<IClientSocket> connectionCallback)
        {
            WaitForConnection(connectionCallback, 10f);
        }

        /// <summary>
        /// Invokes a callback after a successfull connection,
        /// instantly if connected, or after the timeout, if failed to connect
        /// </summary>
        public void WaitForConnection(Action<IClientSocket> connectionCallback, float timeoutSeconds)
        {
            if (IsConnected)
            {
                connectionCallback.Invoke(this);
                return;
            }

            var isConnected = false;
            var timedOut = false;
            Action onConnected = null;
            onConnected = () =>
            {
                OnConnectedEvent -= onConnected;
                isConnected = true;

                if (!timedOut)
                {
                    connectionCallback.Invoke(this);
                }
            };

            OnConnectedEvent += onConnected;

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

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        public IPacketHandler RegisterMessageHandler(IPacketHandler handler)
        {
            _handlers[handler.OpCode] = handler;
            return handler;
        }

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        public IPacketHandler RegisterMessageHandler(short opCode, IncommingMessageHandler handlerMethod)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            RegisterMessageHandler(handler);
            return handler;
        }

        /// <summary>
        /// Removes the packet handler, but only if this exact handler
        /// was used
        /// </summary>
        /// <param name="handler"></param>
        public void RemoveMessageHandler(IPacketHandler handler)
        {
            IPacketHandler previousHandler;
            _handlers.TryGetValue(handler.OpCode, out previousHandler);

            if (previousHandler != handler)
                return;

            _handlers.Remove(handler.OpCode);
        }

        public void Update()
        {
            if (_socketId == -1)
                return;

            byte error;

            if (IsConnecting && !IsConnected)
            {
                // Try connecting

                if (Environment.TickCount > _stopConnectingTick)
                {
                    // Timeout reached
                    StopConnecting();
                    return;
                }

                Status = ConnectionStatus.Connecting;

                if (!_isConnectionPending)
                {
                    // TODO Finish implementing multiple connects 
                    _connectionId = NetworkTransport.Connect(_socketId, _ip, _port, 0, out error);
                    _isConnectionPending = true;

                    if (error != (int)NetworkError.Ok)
                    {
                        StopConnecting();
                        return;
                    }
                }
            }

            NetworkEventType networkEvent;

            if (_socketId == -1) return;

            networkEvent = NetworkTransport.ReceiveFromHost(_socketId,
                out int connectionId,
                out int channelId,
                _msgBuffer,
                _msgBuffer.Length,
                out int receivedSize,
                out error);

            switch (networkEvent)
            {
                case NetworkEventType.ConnectEvent:
                    HandleConnect(connectionId, error);
                    break;
                case NetworkEventType.DataEvent:
                    HandleData(connectionId, channelId, receivedSize, error);
                    break;
                case NetworkEventType.DisconnectEvent:
                    HandleDisconnect(connectionId, error);
                    break;
                case NetworkEventType.Nothing:
                    break;
                default:
                    Logs.Error("Unknown network message type received: " + networkEvent);
                    break;
            }
        }

        private void StopConnecting()
        {
            IsConnecting = false;
            Status = ConnectionStatus.Disconnected;
            MstUpdateRunner.Singleton.Remove(this);
        }

        private void HandleDisconnect(int connectionId, byte error)
        {
            if (_peer != null)
                _peer.Dispose();

            if (_connectionId != connectionId)
                return;

            _isConnectionPending = false;

            MstUpdateRunner.Singleton.Remove(this);

            Status = ConnectionStatus.Disconnected;
            IsConnected = false;
            _socketId = -1;

            if (_peer != null)
            {
                _peer.SetIsConnected(false);
                _peer.NotifyDisconnectEvent();
            }

            if (OnDisconnectedEvent != null)
                OnDisconnectedEvent.Invoke();
        }

        private void HandleData(int connectionId, int channelId, int receivedSize, byte error)
        {
            if (_peer == null)
                return;

            _peer.HandleDataReceived(_msgBuffer, 0);
        }

        private void HandleConnect(int connectionId, byte error)
        {
            if (_connectionId != connectionId)
                return;

            _isConnectionPending = false;

            IsConnecting = false;
            IsConnected = true;

            Status = ConnectionStatus.Connected;

            if (_peer != null)
                _peer.OnMessageReceivedEvent -= HandleMessage;

            _peer = new PeerUnet(connectionId, _socketId, _msgBuffer.Length);
            _peer.SetIsConnected(true);
            _peer.OnMessageReceivedEvent += HandleMessage;

            Peer = _peer;

            OnConnectedEvent?.Invoke();
        }

        private void HandleMessage(IIncomingMessage message)
        {
            try
            {
                _handlers.TryGetValue(message.OpCode, out IPacketHandler handler);

                if (handler != null)
                    handler.Handle(message);
                else if (message.IsExpectingResponse)
                {
                    Logs.Error("Connection is missing a handler. OpCode: " + message.OpCode);
                    message.Respond(ResponseStatus.Error);
                }
            }
            catch (Exception e)
            {
#if UNITY_EDITOR
                if (RethrowExceptionsInEditor)
                    throw;
#endif

                Logs.Error("Failed to handle a message. OpCode: " + message.OpCode);
                Logs.Error(e);

                if (!message.IsExpectingResponse)
                    return;

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

        public void Disconnect(bool fireEvent = true)
        {
            MstUpdateRunner.Singleton.Remove(this);

            if (_socketId > 0 && _socketId < 2)
            {
                NetworkTransport.Disconnect(_socketId, _connectionId, out byte error);
                NetworkTransport.RemoveHost(_socketId);

                // When we disconnect ourselves, we dont get NetworkEventType.DisconnectEvent 
                // Not sure if that's the expected behaviour, but oh well...
                // TODO Make sure there's no other way
                HandleDisconnect(_connectionId, error);
            }

            if (_peer != null)
            {
                _peer.OnMessageReceivedEvent -= HandleMessage;
                _peer.Dispose();
            }

            IsConnected = false;
            Status = ConnectionStatus.Disconnected;
        }
    }
}