using MasterServerToolkit.Networking;
using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// This class is a central class, which can be used by entities (clients and servers)
    /// that need to connect to master server, and access it's functionality
    /// </summary>
    public class Mst
    {
        private static IClientSocket _connection;
        private static MstAdvancedSettings _settings;
        private static MstServer _server;
        private static MstClient _client;
        private static MstConcurrency _concurrency;
        private static MstCreate _create;
        private static MstHelper _helper;
        private static MstSecurity _security;
        private static MstEventsChannel _events;
        private static MstRuntime _runtime;
        private static MstArgs _args;
        private static MstAnalytics _analytics;
        private static MstProperties _options;

        /// <summary>
        /// Version of the framework
        /// </summary>
        public static string Version => "4.14";

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
        /// Main connection to master server
        /// </summary>
        public static IClientSocket Connection
        {
            get
            {
                _connection ??= Create.ClientSocket();
                return _connection;
            }
        }

        /// <summary>
        /// Advanced master server framework settings
        /// </summary>
        public static MstAdvancedSettings Settings
        {
            get
            {
                _settings ??= new MstAdvancedSettings();
                return _settings;
            }
        }

        /// <summary>
        /// Collection of methods, that can be used BY CLIENT, connected to master server
        /// </summary>
        public static MstClient Client
        {
            get
            {
                _client ??= new MstClient(Connection);
                return _client;
            }
        }

        /// <summary>
        /// Collection of methods, that can be used from your servers
        /// </summary>
        public static MstServer Server
        {
            get
            {
                _server ??= new MstServer(Connection);
                return _server;
            }
        }

        /// <summary>
        /// Contains methods to help work with threads
        /// </summary>
        public static MstConcurrency Concurrency
        {
            get
            {
                _concurrency ??= new MstConcurrency();
                return _concurrency;
            }
        }

        /// <summary>
        /// Contains methods for creating some of the common types
        /// (server sockets, messages and etc)
        /// </summary>
        public static MstCreate Create
        {
            get
            {
                _create ??= new MstCreate();
                return _create;
            }
        }

        /// <summary>
        /// Contains helper methods, that couldn't be added to any other
        /// object
        /// </summary>
        public static MstHelper Helper
        {
            get
            {
                _helper ??= new MstHelper();
                return _helper;
            }
        }

        /// <summary>
        /// Contains security-related stuff (encryptions, permission requests)
        /// </summary>
        public static MstSecurity Security
        {
            get
            {
                _security ??= new MstSecurity(Connection);
                return _security;
            }
        }

        /// <summary>
        /// Default events channel
        /// </summary>
        public static MstEventsChannel Events
        {
            get
            {
                _events ??= new MstEventsChannel();
                return _events;
            }
        }

        /// <summary>
        /// Contains methods, that work with runtime data
        /// </summary>
        public static MstRuntime Runtime
        {
            get
            {
                _runtime ??= new MstRuntime();
                return _runtime;
            }
        }

        /// <summary>
        /// Contains command line / terminal values, which were provided
        /// when starting the process
        /// </summary>
        public static MstArgs Args
        {
            get
            {
                _args ??= new MstArgs();
                return _args;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static MstAnalytics Analytics
        {
            get
            {
                _analytics ??= new MstAnalytics();
                return _analytics;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static MstProperties Options
        {
            get
            {
                _options ??= new MstProperties();
                return _options;
            }
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RunOnStart()
        {
            _connection = null;
            _settings = null;
            _server = null;
            _client = null;
            _concurrency = null;
            _create = null;
            _helper = null;
            _security = null;
            _events = null;
            _runtime = null;
            _args = null;
            _analytics = null;
            _options = null;
        }
#endif
    }
}