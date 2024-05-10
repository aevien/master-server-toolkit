namespace MasterServerToolkit.Networking
{
    public delegate void ConnectionDelegate(IClientSocket client);
    public delegate void ConnectionStatusDelegate(ConnectionStatus status);

    public interface IClientSocket : IMsgDispatcher
    {
        /// <summary>
        /// The code that the client receives when the connection is closed
        /// </summary>
        ushort CloseCode { get; }

        /// <summary>
        /// Connection status
        /// </summary>
        ConnectionStatus Status { get; }

        /// <summary>
        /// Returns true, if we are connected to another socket
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Returns true, if we're in the process of connecting
        /// </summary>
        bool IsConnecting { get; }

        /// <summary>
        /// Ip of the server to which we're connected
        /// </summary>
        string Address { get; }

        /// <summary>
        /// Port of the server to which we're connected
        /// </summary>
        int Port { get; }

        /// <summary>
        /// Whether or not to use secure connection
        /// </summary>
        bool UseSecure { get; set; }

        /// <summary>
        /// The service to which the client can be connected
        /// </summary>
        string Service { get; set; }

        /// <summary>
        /// The password that the connection will use to authenticate to the server
        /// </summary>
        string Password { get; set; }

        /// <summary>
        /// Event, which is invoked when we successfully 
        /// connect to another socket
        /// </summary>
        event ConnectionDelegate OnConnectionOpenEvent;

        /// <summary>
        /// Event, which is invoked when we are
        /// disconnected from another socket with normal code
        /// </summary>
        event ConnectionDelegate OnConnectionCloseEvent;

        /// <summary>
        /// Event, invoked when connection status changes
        /// </summary>
        event ConnectionStatusDelegate OnStatusChangedEvent;

        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeoutMillis"></param>
        /// <returns></returns>
        IClientSocket Connect(string ip, int port, float timeoutSeconds);

        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        IClientSocket Connect(string ip, int port);

        /// <summary>
        /// Invokes a callback when connection is established, or after the timeout
        /// (even if failed to connect). If already connected, callback is invoked instantly
        /// </summary>
        /// <param name="connectionCallback"></param>
        /// <param name="timeoutSeconds"></param>
        void WaitForConnection(ConnectionDelegate connectionCallback, float timeoutSeconds);

        /// <summary>
        /// Invokes a callback when connection is established, or after the timeout
        /// (even if failed to connect). If already connected, callback is invoked instantly
        /// </summary>
        /// <param name="connectionCallback"></param>
        void WaitForConnection(ConnectionDelegate connectionCallback);

        /// <summary>
        /// Adds a listener, which is invoked when connection is established,
        /// or instantly, if already connected and <paramref name="invokeInstantlyIfConnected"/>
        /// is true
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="invokeInstantlyIfConnected"></param>
        void AddConnectionOpenListener(ConnectionDelegate callback, bool invokeInstantlyIfConnected = true);

        /// <summary>
        /// Removes connection listener
        /// </summary>
        /// <param name="callback"></param>
        void RemoveConnectionOpenListener(ConnectionDelegate callback);

        /// <summary>
        /// Adds listener, which is invoked when connection is closed,
        /// or instantly, if already disconnected and <paramref name="invokeInstantlyIfDisconnected"/>
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="invokeInstantlyIfDisconnected"></param>
        void AddConnectionCloseListener(ConnectionDelegate callback, bool invokeInstantlyIfDisconnected = true);

        /// <summary>
        /// Removes disconnection listener
        /// </summary>
        /// <param name="callback"></param>
        void RemoveConnectionCloseListener(ConnectionDelegate callback);

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        IPacketHandler RegisterMessageHandler(IPacketHandler handler);

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        IPacketHandler RegisterMessageHandler(ushort opCode, IncommingMessageHandler handlerMethod);

        /// <summary>
        /// Removes the packet handler, but only if this exact handler
        /// was used
        /// </summary>
        /// <param name="handler"></param>
        void RemoveMessageHandler(IPacketHandler handler);

        /// <summary>
        /// Disconnects and connects again
        /// </summary>
        void Reconnect(bool fireEvent = true);

        /// <summary>
        /// Closes socket connection
        /// </summary>
        /// <param name="fireEvent"></param>
        void Close(bool fireEvent = true);

        /// <summary>
        /// Closes socket connection
        /// </summary>
        /// <param name="code"></param>
        /// <param name="fireEvent"></param>
        void Close(ushort code, bool fireEvent = true);

        /// <summary>
        /// Closes socket connection
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        /// <param name="fireEvent"></param>
        void Close(ushort code, string reason, bool fireEvent = true);
    }
}