using System;

namespace MasterServerToolkit.Networking
{
    public interface IClientSocket : IMsgDispatcher
    {
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
        string ConnectionIp { get; }

        /// <summary>
        /// Port of the server to which we're connected
        /// </summary>
        int ConnectionPort { get; }

        /// <summary>
        /// Whether or not to use secure connection
        /// </summary>
        bool UseSecure { get; set; }

        /// <summary>
        /// Event, which is invoked when we successfully 
        /// connect to another socket
        /// </summary>
        event Action OnConnectedEvent;

        /// <summary>
        /// Event, which is invoked when we are
        /// disconnected from another socket
        /// </summary>
        event Action OnDisconnectedEvent;

        /// <summary>
        /// Event, invoked when connection status changes
        /// </summary>
        event Action<ConnectionStatus> OnStatusChangedEvent;

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
        void WaitForConnection(Action<IClientSocket> connectionCallback, float timeoutSeconds);

        /// <summary>
        /// Invokes a callback when connection is established, or after the timeout
        /// (even if failed to connect). If already connected, callback is invoked instantly
        /// </summary>
        /// <param name="connectionCallback"></param>
        void WaitForConnection(Action<IClientSocket> connectionCallback);

        /// <summary>
        /// Adds a listener, which is invoked when connection is established,
        /// or instantly, if already connected and <paramref name="invokeInstantlyIfConnected"/>
        /// is true
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="invokeInstantlyIfConnected"></param>
        void AddConnectionListener(Action callback, bool invokeInstantlyIfConnected = true);

        /// <summary>
        /// Removes connection listener
        /// </summary>
        /// <param name="callback"></param>
        void RemoveConnectionListener(Action callback);

        /// <summary>
        /// Adds listener, which is invoked when connection is closed,
        /// or instantly, if already disconnected and <paramref name="invokeInstantlyIfDisconnected"/>
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="invokeInstantlyIfDisconnected"></param>
        void AddDisconnectionListener(Action callback, bool invokeInstantlyIfDisconnected = true);

        /// <summary>
        /// Removes disconnection listener
        /// </summary>
        /// <param name="callback"></param>
        void RemoveDisconnectionListener(Action callback);

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        IPacketHandler RegisterMessageHandler(IPacketHandler handler);

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        IPacketHandler RegisterMessageHandler(short opCode, IncommingMessageHandler handlerMethod);

        /// <summary>
        /// Removes the packet handler, but only if this exact handler
        /// was used
        /// </summary>
        /// <param name="handler"></param>
        void RemoveMessageHandler(IPacketHandler handler);

        /// <summary>
        /// Disconnects and connects again
        /// </summary>
        void Reconnect();

        /// <summary>
        /// Closes socket connection
        /// </summary>
        void Disconnect(bool fireEvent = true);
    }
}