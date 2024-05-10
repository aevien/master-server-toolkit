using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using WebSocketSharp.Server;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Server socket, which accepts websocket connections
    /// </summary>
    public class WsServerSocket : IServerSocket
    {
        private WebSocketServer server;
        private readonly Logger logger;
        private LogLevel logLevel = LogLevel.Info;

        public bool UseSecure { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }
        public string Service { get; set; } = "mst";
        public SslProtocols SslProtocols { get; set; }
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

        public event PeerActionHandler OnPeerConnectedEvent;
        public event PeerActionHandler OnPeerDisconnectedEvent;
        public event Action<IServerSocket> OnBeforeServerStart;

        public WsServerSocket()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
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
        public void Listen(string address, int port)
        {
            try
            {
                server?.Stop();

                if (address.Trim() == "localhost")
                {
                    server = new WebSocketServer(port, UseSecure);
                }
                else if (IPAddress.TryParse(address, out var ipAddress))
                {
                    server = new WebSocketServer(ipAddress, port, UseSecure);
                }
                else
                {
                    string url = $"{(UseSecure ? "wss://" : "ws://")}{address}:{port}";
                    server = new WebSocketServer(url);
                }

                server.KeepClean = true;

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

                    server.SslConfiguration.EnabledSslProtocols = SslProtocols;
                }

                // Setup all services used by server
                SetupService(server);

                // Setup something else if required
                OnBeforeServerStart?.Invoke(this);

                // Start server
                server.Start();
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
            server?.Stop();
        }

        /// <summary>
        /// Setup all services used by server
        /// </summary>
        /// <param name="server"></param>
        private void SetupService(WebSocketServer server)
        {
            // Master server service
            server.AddWebSocketService<WsService>($"/{Service}", (serviceForPeer) =>
            {
                var peer = new WsServerPeer(serviceForPeer)
                {
                    LogLevel = logLevel
                };

                peer.OnConnectionOpenEvent += (peer) =>
                {
                    OnPeerConnectedEvent?.Invoke(peer);
                };

                peer.OnConnectionCloseEvent += (peer) =>
                {
                    OnPeerDisconnectedEvent?.Invoke(peer);
                };
            });

            // Echo test service
            server.AddWebSocketService<EchoService>("/echo", (echoService) =>
            {
                echoService.IgnoreExtensions = true;
            });
        }
    }
}