using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MasterServerToolkit.Networking.Unet
{
    /// <summary>
    /// Represents a socket, which listen to a port, and to which
    /// other clients can connect
    /// </summary>
    public class UnetServerSocket : IServerSocket, IUpdatable
    {
        private readonly HostTopology _topology;
        private readonly Dictionary<int, PeerUnet> _connectedPeers;
        private readonly byte[] _msgBuffer;
        private int _socketId = -1;

        public bool UseSecure { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }
        public string ApplicationKey { get; set; }
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        public event PeerActionHandler OnPeerConnectedEvent;
        public event PeerActionHandler OnPeerDisconnectedEvent;
        public event Action<IServerSocket> OnBeforeServerStart;

        public UnetServerSocket() : this(UnetSocketTopology.Topology) { }

        public UnetServerSocket(HostTopology topology)
        {
            _connectedPeers = new Dictionary<int, PeerUnet>();
            _topology = topology;
            _msgBuffer = new byte[0xFFFF];
        }

        public void Listen(int port)
        {
            Listen("127.0.0.1", port);
        }

        public void Listen(string ip, int port)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // Setup something else if needed
            OnBeforeServerStart?.Invoke(this);

            NetworkTransport.Init();
            _socketId = NetworkTransport.AddHost(_topology, port, ip);
            MstUpdateRunner.Singleton.Add(this);
#endif
        }

        public event PeerActionHandler OnConnected
        {
            add { OnPeerConnectedEvent += value; }
            remove { OnPeerConnectedEvent -= value; }
        }

        public event PeerActionHandler OnDisconnected
        {
            add { OnPeerDisconnectedEvent += value; }
            remove { OnPeerDisconnectedEvent -= value; }
        }

        public void Update()
        {
            if (_socketId == -1) return;

            NetworkEventType networkEvent;

            networkEvent = NetworkTransport.ReceiveFromHost(_socketId, out int connectionId, out int channelId, _msgBuffer,
                    _msgBuffer.Length, out int receivedSize, out byte error);

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

        private void HandleDisconnect(int connectionId, byte error)
        {
            _connectedPeers.TryGetValue(connectionId, out PeerUnet peer);

            if (peer == null)
                return;

            peer.Dispose();

            _connectedPeers.Remove(connectionId);

            peer.SetIsConnected(false);
            peer.NotifyDisconnectEvent();

            if (OnPeerDisconnectedEvent != null)
                OnPeerDisconnectedEvent.Invoke(peer);
        }

        private void HandleData(int connectionId, int channelId, int receivedSize, byte error)
        {
            _connectedPeers.TryGetValue(connectionId, out PeerUnet peer);

            if (peer != null)
                peer.HandleDataReceived(_msgBuffer, 0);
        }

        private void HandleConnect(int connectionId, byte error)
        {
            if (error != 0)
            {
                Logs.Error(string.Format("Error on ConnectEvent. ConnectionId: {0}, error: {1}", connectionId, error));
                return;
            }

            var peer = new PeerUnet(connectionId, _socketId, _msgBuffer.Length);
            peer.SetIsConnected(true);
            _connectedPeers.Add(connectionId, peer);

            peer.SetIsConnected(true);

            if (OnPeerConnectedEvent != null)
                OnPeerConnectedEvent.Invoke(peer);
        }

        public void Stop()
        {
            MstUpdateRunner.Singleton.Remove(this);

#if !UNITY_WEBGL || UNITY_EDITOR
            NetworkTransport.RemoveHost(_socketId);
#endif
        }
    }
}