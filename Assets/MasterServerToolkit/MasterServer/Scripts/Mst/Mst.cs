using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// This class is a central class, which can be used by entities (clients and servers)
    /// that need to connect to master server, and access it's functionality
    /// </summary>
    public static class Mst
    {
        /// <summary>
        /// Version of the framework
        /// </summary>
        public static string Version => "4.13.2";

        /// <summary>
        /// Just name of the framework
        /// </summary>
        public static string Name => "MASTER SERVER TOOLKIT";

        /// <summary>
        /// Root menu of framework
        /// </summary>
        public const string ToolMenu = "Tools/Master Server Toolkit/";

        /// <summary>
        /// 
        /// </summary>
        public const string CreateMenu = "Master Server Toolkit/";

        /// <summary>
        /// Check if MST in dev mode
        /// </summary>
        public static bool UseDevMode { get; set; }

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
        public static MstCreate Create { get; set; }

        /// <summary>
        /// Contains helper methods, that couldn't be added to any other
        /// object
        /// </summary>
        public static MstHelper Helper { get; set; }

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
        public static MstAnalytics Analytics { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public static MstProperties Options { get; private set; }

        static Mst()
        {
            // Initialize helpers to work with MSF
            Helper = new MstHelper();

            // Initialize advanced settings
            Settings = new MstAdvancedSettings();

            // Initialize runtime data
            Runtime = new MstRuntime();

            // Initialize work with command line arguments
            Args = new MstArgs();

            // List of options you can use in game
            Options = new MstProperties();

            // Create a default connection
            Connection = Settings.ClientSocketFactory();

            // Initialize parts of framework, that act as "clients"
            Client = new MstClient(Connection);
            Server = new MstServer(Connection);
            Security = new MstSecurity(Connection);

            // Other stuff
            Create = new MstCreate();
            Concurrency = new MstConcurrency();
            Events = new MstEventsChannel("default", true);

            //
            Analytics = new MstAnalytics();

            UseDevMode = Args.UseDevMode;
        }
    }
}