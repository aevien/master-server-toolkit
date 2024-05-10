using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.Bridges.Dashboard
{
    public class DashboardServerBehaviour : ServerBehaviour
    {
        /// <summary>
        /// Singleton instance of the server behaviour
        /// </summary>
        public static DashboardServerBehaviour Instance { get; private set; }

        /// <summary>
        /// Invoked when server started
        /// </summary>
        public static event Action<DashboardServerBehaviour> OnDashboardStartedEvent;

        /// <summary>
        /// Invoked when server stopped
        /// </summary>
        public static event Action<DashboardServerBehaviour> OnDashboardStoppedEvent;

        protected override void Awake()
        {
            base.Awake();

            // If instance of the server is already running
            if (Instance != null)
            {
                // Destroy, if this is not the first instance
                Destroy(gameObject);
                return;
            }

            // Create new instance
            Instance = this;

            // Move to root, so that it won't be destroyed
            // In case this MSF instance is a child of another gameobject
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            // Set server behaviour to be able to use in all levels
            DontDestroyOnLoad(gameObject);

            // If IP is provided via cmd arguments
            serverIp = Mst.Args.AsString(Mst.Args.Names.DashboardIp, serverIp);
            // If port is provided via cmd arguments
            serverPort = Mst.Args.AsInt(Mst.Args.Names.DashboardPort, serverPort);
        }

        protected override void Start()
        {
            base.Start();

            // Start the server on next frame
            MstTimer.WaitForEndOfFrame(() =>
            {
                StartServer();
            });
        }

        protected override void OnStartedServer()
        {
            logger.Info($"{GetType().Name.FromCamelcase()} started and listening to: {serverIp}:{serverPort}");

            base.OnStartedServer();

            OnDashboardStartedEvent?.Invoke(this);
        }

        protected override void OnStoppedServer()
        {
            logger.Info($"{GetType().Name.FromCamelcase()} stopped");

            OnDashboardStoppedEvent?.Invoke(this);
        }
    }
}
