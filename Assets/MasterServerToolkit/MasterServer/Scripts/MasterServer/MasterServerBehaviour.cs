using MasterServerToolkit.Networking;
using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Starts the master server
    /// </summary>
    [AddComponentMenu("Master Server Toolkit/MasterServerBehaviour")]
    public class MasterServerBehaviour : ServerBehaviour
    {
        /// <summary>
        /// Singleton instance of the master server behaviour
        /// </summary>
        public static MasterServerBehaviour Instance { get; private set; }

        /// <summary>
        /// Invoked when master server started
        /// </summary>
        public static event Action<MasterServerBehaviour> OnMasterStartedEvent;

        /// <summary>
        /// Invoked when master server stopped
        /// </summary>
        public static event Action<MasterServerBehaviour> OnMasterStoppedEvent;

        protected override void Awake()
        {
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

            // Check is command line argument '-msfMasterPort' is defined
            if (Mst.Args.IsProvided(Mst.Args.Names.MasterIp))
            {
                serverIP = Mst.Args.MasterIp;
            }

            // Check is command line argument '-msfMasterPort' is defined
            if (Mst.Args.IsProvided(Mst.Args.Names.MasterPort))
            {
                serverPort = Mst.Args.MasterPort;
            }

            base.Awake();
        }

        protected override void Start()
        {
            base.Start();

            // Start master server at start
            if (Mst.Args.StartMaster && !Mst.Runtime.IsEditor)
            {
                // Start the server on next frame
                MstTimer.WaitForEndOfFrame(() =>
                {
                    StartServer();
                });
            }
        }

        /// <summary>
        /// Start master server with given port
        /// </summary>
        public override void StartServer()
        {
            // If master is allready running then return function
            if (IsRunning)
            {
                return;
            }

            logger.Info("Starting Master Server...");
            logger.Info($"Multithreading is: {(Mst.Runtime.SupportsThreads ? "On" : "Off")}");
            logger.Info($"FPS is: {Application.targetFrameRate}");

            base.StartServer();
        }

        protected override void OnStartedServer()
        {
            logger.Info($"Master Server is started and listening to: {serverIP}:{serverPort}");

            base.OnStartedServer();

            OnMasterStartedEvent?.Invoke(this);
        }

        protected override void OnStoppedServer()
        {
            logger.Info("Master Server is stopped");

            OnMasterStoppedEvent?.Invoke(this);
        }
    }
}