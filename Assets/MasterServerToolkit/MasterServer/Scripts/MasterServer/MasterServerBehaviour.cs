using MasterServerToolkit.Extensions;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MasterServerBehaviour : ServerBehaviour
    {
        [NonSerialized]
        private static MasterServerBehaviour instance;

        /// <summary>
        /// Singleton instance of the master server behaviour
        /// </summary>
        public static MasterServerBehaviour Instance => instance;

        /// <summary>
        /// Invoked when master server started
        /// </summary>
        public static event Action<MasterServerBehaviour> OnMasterStartedEvent;

        /// <summary>
        /// Invoked when master server stopped
        /// </summary>
        public static event Action<MasterServerBehaviour> OnMasterStoppedEvent;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RunOnStart()
        {
            instance = null;
            OnMasterStartedEvent = null;
            OnMasterStoppedEvent = null;
        }
#endif

        protected override void Awake()
        {
            base.Awake();

            // If instance of the server is already running
            if (instance != null)
            {
                // Destroy, if this is not the first instance
                Destroy(gameObject);
                return;
            }

            // Create new instance
            instance = this;

            // Move to root, so that it won't be destroyed
            // In case this instance is a child of another gameobject
            if (transform.parent != null)
                transform.SetParent(null);

            // Set server behaviour to be able to use in all levels
            DontDestroyOnLoad(gameObject);

            // If master IP is provided via cmd arguments
            serverIp = Mst.Args.AsString(Mst.Args.Names.MasterIp, serverIp);
            // If master port is provided via cmd arguments
            serverPort = Mst.Args.AsInt(Mst.Args.Names.MasterPort, serverPort);
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
            logger.Info($"{GetType().Name.ToSpaceByUppercase()} started and listening to: {serverIp}:{serverPort}");
            base.OnStartedServer();
            OnMasterStartedEvent?.Invoke(this);
        }

        protected override void OnStoppedServer()
        {
            logger.Info($"{GetType().Name.ToSpaceByUppercase()} stopped");
            OnMasterStoppedEvent?.Invoke(this);
        }
    }
}