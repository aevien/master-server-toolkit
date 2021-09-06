using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
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
        private Logger logger;

        public bool UseSecure { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }
        public string ApplicationKey { get; set; }
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// Invokes before server frame is updated
        /// </summary>
        public event Action OnUpdateEvent;
        public event PeerActionHandler OnPeerConnectedEvent;
        public event PeerActionHandler OnPeerDisconnectedEvent;
        public event Action<IServerSocket> OnBeforeServerStart;

        public WsServerSocket()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = LogLevel;
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
            try
            {
                // Stop listening when application closes
                MstTimer.Singleton.OnApplicationQuitEvent += Stop;

                if (server != null)
                {
                    server.Stop();
                }

                if (ip == "127.0.0.1".Trim() || ip == "localhost".Trim())
                {
                    server = new WebSocketServer(port, UseSecure);
                }
                else
                {
                    server = new WebSocketServer(IPAddress.Parse(ip), port, UseSecure);
                }

                //server.KeepClean = true;

                // Set log output
                server.Log.Output = (logData, value) =>
                {
                    switch (logData.Level)
                    {
                        case WebSocketSharp.LogLevel.Error:
                            logger.Error(logData.Message);
                            break;
                        case WebSocketSharp.LogLevel.Fatal:
                            logger.Fatal(logData.Message);
                            break;
                        case WebSocketSharp.LogLevel.Info:
                            logger.Info(logData.Message);
                            break;
                        case WebSocketSharp.LogLevel.Debug:
                            logger.Debug(logData.Message);
                            break;
                        case WebSocketSharp.LogLevel.Warn:
                            logger.Warn(logData.Message);
                            break;
                        case WebSocketSharp.LogLevel.Trace:
                            logger.Trace(logData.Message);
                            break;
                        default:
                            logger.Info(logData.Message);
                            break;
                    }
                };

                if (UseSecure)
                {
                    if (string.IsNullOrEmpty(CertificatePath.Trim()))
                    {
                        logger.Error("You are using secure connection, but no path to certificate defined. Stop connection process.");
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
                MstUpdateRunner.Singleton.Add(this);
            }
            catch (CryptographicException e)
            {
                logger.Error(e.Message);
            }
            catch (PlatformNotSupportedException e)
            {
                logger.Error(e.Message);
            }
        }

        /// <summary>
        /// Stops listening
        /// </summary>
        public void Stop()
        {
            MstUpdateRunner.Singleton.Remove(this);
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
                    ExecuteOnUpdate(() =>
                    {
                        peer.SendDelayedMessages();
                        OnPeerConnectedEvent?.Invoke(peer);
                    });

                    logger.Debug($"Connection for peer [{peer.Id}] is open");
                };

                serviceForPeer.OnMessageEvent += (data) =>
                {
                    peer.HandleDataReceived(data);
                };

                serviceForPeer.OnCloseEvent += reason =>
                {
                    OnPeerDisconnectedEvent?.Invoke(peer);
                    peer.NotifyDisconnectEvent();
                };

                serviceForPeer.OnErrorEvent += reason =>
                {
                    logger.Error(reason);
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