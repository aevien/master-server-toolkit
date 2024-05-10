using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Advanced settings wrapper
    /// </summary>
    public class MstAdvancedSettings
    {
        /// <summary>
        /// Factory, used to create client sockets
        /// </summary>
        public Func<IClientSocket> ClientSocketFactory = () => new WsClientSocket();

        /// <summary>
        /// Factory, used to create server sockets
        /// </summary>
        public Func<IServerSocket> ServerSocketFactory = () => new WsServerSocket();

        /// <summary>
        /// Global logging settings
        /// </summary>
        public MstLogController Logging { get; private set; }

        public MstAdvancedSettings()
        {
            Logging = new MstLogController(LogLevel.All);
        }
    }
}