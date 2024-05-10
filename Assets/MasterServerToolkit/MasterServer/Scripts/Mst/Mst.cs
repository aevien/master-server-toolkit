using MasterServerToolkit.Localization;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// This class is a central class, which can be used by entities (clients and servers)
    /// that need to connect to master server, and access it's functionality
    /// </summary>
    public partial class Mst
    {
        /// <summary>
        /// Version of the framework
        /// </summary>
        public static string Version => "4.17.0";

        /// <summary>
        /// Just name of the framework
        /// </summary>
        public static string Name => "Master Server Toolkit";

        /// <summary>
        /// Main connection to master server
        /// </summary>
        public static IClientSocket Connection { get; private set; }

        /// <summary>
        /// Advanced master server framework settings
        /// </summary>
        public static MstAdvancedSettings Settings { get; private set; }

        /// <summary>
        /// Collection of methods, that can be used BY CLIENT, connected to master server
        /// </summary>
        public static MstClient Client { get; private set; }

        /// <summary>
        /// Collection of methods, that can be used from your servers
        /// </summary>
        public static MstServer Server { get; private set; }

        /// <summary>
        /// Contains methods to help work with threads
        /// </summary>
        public static MstConcurrency Concurrency { get; private set; }

        /// <summary>
        /// Contains methods for creating some of the common types
        /// (server sockets, messages and etc)
        /// </summary>
        public static MstCreate Create { get; private set; }

        /// <summary>
        /// Contains helper methods, that couldn't be added to any other
        /// object
        /// </summary>
        public static MstHelper Helper { get; private set; }

        /// <summary>
        /// Contains security-related stuff (encryptions, permission requests)
        /// </summary>
        public static MstSecurity Security { get; private set; }

        /// <summary>
        /// Default events channel
        /// </summary>
        public static MstEventsChannel Events { get; private set; }

        /// <summary>
        /// Contains methods, that work with runtime data
        /// </summary>
        public static MstRuntime Runtime { get; private set; }

        /// <summary>
        /// Contains command line / terminal values, which were provided
        /// when starting the process
        /// </summary>
        public static MstArgs Args { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public static MstTrafficStatistics TrafficStatistics { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public static MstProperties Options { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public static MstLocalization Localization { get; private set; }

        static Mst()
        {
            Initialize();
        }

        private static void Initialize()
        {
            Helper = new MstHelper();
            Args = new MstArgs();
            Localization = new MstLocalization();
            Settings = new MstAdvancedSettings();
            Runtime = new MstRuntime();

            Connection = Settings.ClientSocketFactory();
            Client = new MstClient(Connection);
            Server = new MstServer(Connection);
            Security = new MstSecurity(Connection);

            Create = new MstCreate();
            Concurrency = new MstConcurrency();
            Events = new MstEventsChannel();
            TrafficStatistics = new MstTrafficStatistics();
            Options = new MstProperties();
        }
    }
}