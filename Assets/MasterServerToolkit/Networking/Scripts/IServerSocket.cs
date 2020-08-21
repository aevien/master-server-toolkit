namespace MasterServerToolkit.Networking
{
    public delegate void PeerActionHandler(IPeer peer);

    public interface IServerSocket
    {
        /// <summary>
        /// Invoked, when a client connects to this socket
        /// </summary>
        event PeerActionHandler OnPeerConnectedEvent;

        /// <summary>
        /// Invoked, when client disconnects from this socket
        /// </summary>
        event PeerActionHandler OnPeerDisconnectedEvent;

        /// <summary>
        /// Whether  or not to use secure connection
        /// </summary>
        bool UseSsl { get; set; }

        /// <summary>
        /// Path to certificate
        /// </summary>
        string CertificatePath { get; set; }

        /// <summary>
        /// Your certificate password
        /// </summary>
        string CertificatePassword { get; set; }

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