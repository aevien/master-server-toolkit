using arebones.Networking;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using WebSocketSharp.Server;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Server socket, which accepts websocket connections
    /// </summary>
    public partial class ServerSocketWs : IServerSocket, IUpdatable
    {
        private WebSocketServer server;
        private Queue<Action> executeOnUpdate;
        private float initialSendMessageDelayTime = 0.2f;

        public bool UseSsl { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }

        /// <summary>
        /// Invokes before server frame is updated
        /// </summary>
        public event Action OnUpdateEvent;

        /// <summary>
        /// Invoked, when a client connects to this socket
        /// </summary>
        public event PeerActionHandler OnPeerConnectedEvent;

        /// <summary>
        /// Invoked, when client disconnects from this socket
        /// </summary>
        public event PeerActionHandler OnPeerDisconnectedEvent;

        public ServerSocketWs()
        {
            executeOnUpdate = new Queue<Action>();
        }

        /// <summary>
        /// Opens the socket and starts listening to a given port. IP is 127.0.0.1
        /// </summary>
        /// <param name="port"></param>
        public void Listen(int port)
        {
            Listen("127.0.0.1", port);
        }

        /// <summary>
        /// Opens the socket and starts listening to a given port and IP
        /// </summary>
        /// <param name="port"></param>
        public void Listen(string ip, int port)
        {
            // Stop listening when application closes
            MstTimer.Instance.OnApplicationQuitEvent += Stop;

            if(ip == "127.0.0.1" | ip == "localhost")
            {
                server = new WebSocketServer(port, UseSsl);
            }
            else
            {
                server = new WebSocketServer(IPAddress.Parse(ip), port, UseSsl);
            }

            if (UseSsl)
            {
                if (string.IsNullOrEmpty(CertificatePath.Trim())) {
                    Logs.Error("You are using secure connection, but no path to certificate defined. Stop connection process.");
                    return;
                }

                server.SslConfiguration.ServerCertificate = new X509Certificate2(CertificatePath, CertificatePassword);
            }

            SetupService(server);

            server.Stop();
            server.Start();

            MstUpdateRunner.Instance.Add(this);
        }

        /// <summary>
        /// Stops listening
        /// </summary>
        public void Stop()
        {
            MstUpdateRunner.Instance.Remove(this);
            server.Stop();
        }

        public void ExecuteOnUpdate(Action action)
        {
            lock (executeOnUpdate)
            {
                executeOnUpdate.Enqueue(action);
            }
        }

        private void SetupService(WebSocketServer server)
        {
            server.AddWebSocketService<WsService>("/msf", (service) =>
            {
                service.IgnoreExtensions = true;
                service.SetServerSocket(this);
                var peer = new PeerWsServer(service);

                service.OnMessageEvent += (data) =>
                {
                    peer.HandleDataReceived(data, 0);
                };

                ExecuteOnUpdate(() =>
                {
                    MstTimer.Instance.StartCoroutine(peer.SendDelayedMessages(initialSendMessageDelayTime));
                    OnPeerConnectedEvent?.Invoke(peer);
                });

                peer.OnPeerDisconnectedEvent += OnPeerDisconnectedEvent;

                service.OnCloseEvent += reason =>
                {
                    peer.NotifyDisconnectEvent();
                };

                service.OnErrorEvent += reason =>
                {
                    Logs.Error(reason);
                    peer.NotifyDisconnectEvent();
                };
            });

            server.AddWebSocketService<EchoService>("/echo");
        }

        public void Update()
        {
            OnUpdateEvent?.Invoke();

            lock (executeOnUpdate)
            {
                while (executeOnUpdate.Count > 0)
                {
                    executeOnUpdate.Dequeue()?.Invoke();
                }
            }
        }
    }
}