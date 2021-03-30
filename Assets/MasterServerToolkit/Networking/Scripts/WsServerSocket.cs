using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp.Server;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Server socket, which accepts websocket connections
    /// </summary>
    public partial class WsServerSocket : IServerSocket, IUpdatable
    {
        private WebSocketServer server;
        private readonly Queue<Action> executeOnUpdate;
        private readonly float initialSendMessageDelayTime = 0.2f;

        public bool UseSecure { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }
        public string ApplicationKey { get; set; }

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

        /// <summary>
        /// Invokes before server starts
        /// </summary>
        public event Action<IServerSocket> OnBeforeServerStart;

        public WsServerSocket()
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

            if (server != null)
            {
                server.Stop();
            }

            if (ip == "127.0.0.1" | ip == "localhost")
            {
                server = new WebSocketServer(port, UseSecure);
            }
            else
            {
                server = new WebSocketServer(IPAddress.Parse(ip), port, UseSecure);
            }

            if (UseSecure)
            {
                if (string.IsNullOrEmpty(CertificatePath.Trim()))
                {
                    Logs.Error("You are using secure connection, but no path to certificate defined. Stop connection process.");
                    return;
                }

                if (string.IsNullOrEmpty(CertificatePassword.Trim()))
                    server.SslConfiguration.ServerCertificate = new X509Certificate2(CertificatePath);
                else
                    server.SslConfiguration.ServerCertificate = new X509Certificate2(CertificatePath, CertificatePassword);

                server.SslConfiguration.EnabledSslProtocols =
                    System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Ssl3
                    | System.Security.Authentication.SslProtocols.Default;
            }

            // Setup all services used by server
            SetupService(server);

            // Setup something else if needed
            OnBeforeServerStart?.Invoke(this);

            // Start server
            server.Start();

            // Add this server to updater
            MstUpdateRunner.Instance.Add(this);
        }

        /// <summary>
        /// Stops listening
        /// </summary>
        public void Stop()
        {
            MstUpdateRunner.Instance.Remove(this);
            server?.Stop();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        public void ExecuteOnUpdate(Action action)
        {
            lock (executeOnUpdate)
            {
                executeOnUpdate.Enqueue(action);
            }
        }

        /// <summary>
        /// Setup all services used by server
        /// </summary>
        /// <param name="server"></param>
        private void SetupService(WebSocketServer server)
        {
            // Master server service
            server.AddWebSocketService<WsService>($"/app/{ApplicationKey}", (serviceForPeer) =>
            {
                serviceForPeer.IgnoreExtensions = true;
                serviceForPeer.SetServerSocket(this);

                var peer = new WsServerPeer(serviceForPeer);

                serviceForPeer.OnOpenEvent += () =>
                {
                    Logs.Debug($"Connection for peer [{peer.Id}] is open");
                };

                serviceForPeer.OnMessageEvent += (data) =>
                {
                    peer.HandleDataReceived(data);
                };

                ExecuteOnUpdate(() =>
                {
                    //MstTimer.Instance.StartCoroutine(peer.SendDelayedMessages(initialSendMessageDelayTime));
                    OnPeerConnectedEvent?.Invoke(peer);
                });

                serviceForPeer.OnCloseEvent += reason =>
                {
                    OnPeerDisconnectedEvent?.Invoke(peer);
                    peer.NotifyDisconnectEvent();
                };

                serviceForPeer.OnErrorEvent += reason =>
                {
                    Logs.Error(reason);
                    peer.NotifyDisconnectEvent();
                };
            });

            // Echo test service
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