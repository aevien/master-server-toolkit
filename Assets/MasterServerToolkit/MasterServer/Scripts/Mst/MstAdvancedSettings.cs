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
        private string applicationKey = "mst";
        private bool useSecure = false;

        /// <summary>
        /// 
        /// </summary>
        public string ApplicationKey
        {
            get
            {
                return Mst.Args.AsString(Mst.Args.Names.ApplicationKey, applicationKey).Trim();
            }
            set
            {
                applicationKey = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool HasApplicationKey => !string.IsNullOrEmpty(ApplicationKey);

        /// <summary>
        /// Whether or not to use secure connection 
        /// </summary>
        public bool UseSecure
        {
            get
            {
                return Mst.Args.AsBool(Mst.Args.Names.UseSecure, useSecure);
            }
            set
            {
                useSecure = value;
            }
        }

        /// <summary>
        /// Path to certificate
        /// </summary>
        public string CertificatePath
        {
            get
            {
                return Mst.Args.CertificatePath.Trim();
            }
        }

        /// <summary>
        /// Path to certificate
        /// </summary>
        public string CertificatePassword
        {
            get
            {
                return Mst.Args.CertificatePassword.Trim();
            }
        }

        /// <summary>
        /// Factory, used to create client sockets
        /// </summary>
        public Func<IClientSocket> ClientSocketFactory = () => new WsClientSocket();

        /// <summary>
        /// Factory, used to create server sockets
        /// </summary>
        public Func<IServerSocket> ServerSocketFactory = () => new WsServerSocket();

        /// <summary>
        /// Message factory
        /// </summary>
        public IMessageFactory MessageFactory => new MessageFactory();

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