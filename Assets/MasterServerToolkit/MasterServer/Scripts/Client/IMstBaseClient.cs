using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public interface IMstBaseClient
    {
        Logger Logger { get; set; }

        /// <summary>
        /// Current module connection
        /// </summary>
        IClientSocket Connection { get; }

        /// <summary>
        /// Check if current module is connected to server
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Clears connection and all its handlers if <paramref name="clearHandlers"/> is true
        /// </summary>
        void ClearConnection(bool clearHandlers = true);

        /// <summary>
        /// Sets a message handler to connection, which is used by this this object
        /// to communicate with server
        /// </summary>
        /// <param name="handler"></param>
        void RegisterMessageHandler(IPacketHandler handler);

        /// <summary>
        /// Sets a message handler to connection, which is used by this this object
        /// to communicate with server 
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handler"></param>
        void RegisterMessageHandler(ushort opCode, IncommingMessageHandler handler);

        /// <summary>
        /// Removes the packet handler, but only if this exact handler
        /// was used
        /// </summary>
        /// <param name="handler"></param>
        void UnregisterMessageHandler(IPacketHandler handler);

        /// <summary>
        /// Changes the connection object, and sets all of the message handlers of this object
        /// to new connection.
        /// </summary>
        /// <param name="socket"></param>
        void ChangeConnection(IClientSocket socket);

        /// <summary>
        /// Cast this client behaviour to derived class <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T CastTo<T>() where T : class;
    }
}
