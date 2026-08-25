using MasterServerToolkit.Localization;
using MasterServerToolkit.Networking;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// This class is a central class, which can be used by entities (clients and servers)
    /// that need to connect to master server, and access it's functionality
    /// </summary>
    public partial class Mst
    {
        private static IClientSocket connection;
        private static bool isInitialized;

        /// <summary>
        /// Version of the framework
        /// </summary>
        public static string Version => "5.0.0";

        /// <summary>
        /// Just name of the framework
        /// </summary>
        public static string Name => "Master Server Toolkit";

        /// <summary>
        /// Main connection to master server
        /// </summary>
        public static IClientSocket Connection => connection;

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
        public static MstTrafficStatistics Traffic { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public static MstProperties Options { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public static MstLocalization Localization { get; private set; }

        /// <summary>
        /// Converts structured MST errors into localized client-facing messages.
        /// </summary>
        public static MstErrorParser Errors { get; private set; }

        /// <summary>
        /// Executes actions on Unity main thread
        /// </summary>
        public static MstThread Thread { get; private set; }

        static Mst()
        {
#if UNITY_EDITOR
            RegisterEditorPlayModeReset();
#endif
            Initialize();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeUnityRuntime()
        {
#if UNITY_EDITOR
            ResetRuntimeState();
#endif
            Initialize();
        }

        private static void Initialize()
        {
            if (isInitialized)
                return;

            Helper = new MstHelper();
            Args = new MstArgs();
            Localization = new MstLocalization();
            Errors = new MstErrorParser();
            Settings = new MstAdvancedSettings();
            Runtime = new MstRuntime();

            Create = new MstCreate();
            Events = new MstEventsChannel();
            Traffic = new MstTrafficStatistics();
            Options = new MstProperties();

            Thread = new MstThread();
            Thread.Initialize();

            if (connection == null)
                connection = Settings.ClientSocketFactory();

            Client = new MstClient(Connection);
            Server = new MstServer(Connection);
            Security = new MstSecurity(Connection);

            isInitialized = true;
        }

        private static void ResetRuntimeState()
        {
            Client = null;
            Server = null;
            Security?.Dispose();
            Security = null;

            if (connection != null)
            {
                connection.Close(false);
                connection = null;
            }

            MstLogController.DisposeActiveAppenders();
            MasterServerToolkit.Logging.LogManager.Reset();

            Helper = null;
            Args = null;
            Localization = null;
            Settings = null;
            Runtime = null;
            Create = null;
            Events = null;
            Traffic = null;
            Options = null;
            Thread = null;
            Errors = null;

            isInitialized = false;
        }

#if UNITY_EDITOR
        private static void RegisterEditorPlayModeReset()
        {
            EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
        }

        private static void OnEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    ResetRuntimeState();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    ResetRuntimeState();
                    Initialize();
                    break;
            }
        }
#endif
    }
}

