using MasterServerToolkit.Logging;
using System;
using System.Security.Authentication;

namespace MasterServerToolkit.Networking
{
    public delegate void PeerActionHandler(IPeer peer);

    public interface IServerSocket
    {
        /// <summary>
        /// Invokes before server starts
        /// </summary>
        event Action<IServerSocket> OnBeforeServerStart;

        /// <summary>
        /// Whether or not to use secure connection
        /// </summary>
        bool UseSecure { get; set; }

        /// <summary>
        /// Path to SSL certificate
        /// </summary>
        string CertificatePath { get; set; }

        /// <summary>
        /// Password for SSL certificate
        /// </summary>
        string CertificatePassword { get; set; }

        /// <summary>
        /// Applications key
        /// </summary>
        string Service { get; set; }

        /// <summary>
        /// Ssl Protocols
        /// </summary>
        SslProtocols SslProtocols { get; set; }

        /// <summary>
        /// Log level of the server socket
        /// </summary>
        LogLevel LogLevel { get; set; }

        /// <summary>
        /// Invoked, when a client connects to this socket
        /// </summary>
        event PeerActionHandler OnPeerConnectedEvent;

        /// <summary>
        /// Invoked, when client disconnects from this socket
        /// </summary>
        event PeerActionHandler OnPeerDisconnectedEvent;

        /// <summary>
        /// Opens the socket and starts listening to a given port. IP is 127.0.0.1
        /// </summary>
        /// <param name="port"></param>
        void Listen(int port);

        /// <summary>
        /// Opens the socket and starts listening to a given port and IP
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        void Listen(string ip, int port);

        /// <summary>
        /// Stops listening
        /// </summary>
        void Stop();
    }
}