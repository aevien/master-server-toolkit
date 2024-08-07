﻿using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public abstract class MstBaseClient : IMstBaseClient
    {
        /// <summary>
        /// Client handlers list. Requires for connection changing process. <seealso cref="ChangeConnection(IClientSocket)"/>
        /// </summary>
        protected readonly Dictionary<ushort, IPacketHandler> handlers = new Dictionary<ushort, IPacketHandler>();

        /// <summary>
        /// Logger of current module
        /// </summary>
        public Logger Logger { get; set; }

        /// <summary>
        /// Current module connection
        /// </summary>
        public IClientSocket Connection { get; protected set; }

        /// <summary>
        /// Check if current module is connected to server
        /// </summary>
        public bool IsConnected => Connection != null && Connection.IsConnected;

        public MstBaseClient(IClientSocket connection)
        {
            ChangeConnection(connection, true);
        }

        /// <summary>
        /// Clears connection and all its handlers if <paramref name="clearHandlers"/> is true
        /// </summary>
        public virtual void ClearConnection(bool clearHandlers = true)
        {
            if (Connection != null)
            {
                if (handlers != null && clearHandlers)
                {
                    foreach (var handler in handlers.Values)
                    {
                        Connection.UnregisterMessageHandler(handler);
                    }

                    handlers.Clear();
                }

                Connection.OnStatusChangedEvent -= OnConnectionStatusChanged;
            }
        }

        /// <summary>
        /// Sets a message handler to connection, which is used by this this object
        /// to communicate with server
        /// </summary>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(IPacketHandler handler)
        {
            handlers[handler.OpCode] = Connection?.RegisterMessageHandler(handler);
        }

        /// <summary>
        /// Sets a message handler to connection, which is used by this this object
        /// to communicate with server 
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(ushort opCode, IncommingMessageHandler handler)
        {
            RegisterMessageHandler(new PacketHandler(opCode, handler));
        }

        /// <summary>
        /// Removes the packet handler, but only if this exact handler
        /// was used
        /// </summary>
        /// <param name="handler"></param>
        public void UnregisterMessageHandler(IPacketHandler handler)
        {
            Connection?.UnregisterMessageHandler(handler);
        }

        /// <summary>
        /// Changes the connection object, and sets all of the message handlers of this object
        /// to new connection.
        /// </summary>
        /// <param name="socket"></param>
        public void ChangeConnection(IClientSocket socket, bool clearHandlers = false)
        {
            if (Connection == socket) return;

            // Clear just connection but not handlers
            ClearConnection(clearHandlers);

            // Change connections
            Connection = socket;

            // Override packet handlers
            foreach (var packetHandler in handlers.Values)
            {
                socket.RegisterMessageHandler(packetHandler);
            }

            Connection.OnStatusChangedEvent += OnConnectionStatusChanged;
            OnConnectionSocketChanged(Connection);
        }

        /// <summary>
        /// Fires when connection of this module is changing
        /// </summary>
        /// <param name="socket"></param>
        protected virtual void OnConnectionSocketChanged(IClientSocket socket) { }

        /// <summary>
        /// Fires each time the connection status is changing
        /// </summary>
        /// <param name="status"></param>
        protected virtual void OnConnectionStatusChanged(ConnectionStatus status) { }
    }
}