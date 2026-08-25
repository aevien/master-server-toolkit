using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.MasterServer
{
    public class RoomServer : ServerBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField, Tooltip("Room process manager that registers this room with the master and owns joined-player state. When None, the component searches the same GameObject at startup.")]
        private RoomServerManager roomServerManager;

        #endregion

        /// <summary>
        /// Singleton instance of the room server behaviour
        /// </summary>
        public static RoomServer Instance { get; private set; }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorPlayModeState()
        {
            Instance = null;
        }
#endif

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
            // In case this instance is a child of another gameobject
            if (transform.parent != null)
                transform.SetParent(null);

            // Set server behaviour to be able to use in all levels
            DontDestroyOnLoad(gameObject);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (ReferenceEquals(Instance, this))
                Instance = null;
        }

        protected override void Start()
        {
            base.Start();

            RegisterMessageHandler(MstOpCodes.ValidateRoomAccessRequest, ValidateRoomAccessRequestHandler);
        }

        public override void StartServer()
        {
            // Find room server if it is not assigned in inspector
            if (!roomServerManager) roomServerManager = GetComponent<RoomServerManager>();

            //// Set the max number of connections
            //maxConnections = roomServerManager.RoomOptions.MaxConnections;

            //// Start server with room options
            //StartServer(roomServerManager.RoomOptions.RoomIp, roomServerManager.RoomOptions.RoomPort);
        }

        protected override void OnStartedServer()
        {
            base.OnStartedServer();

            string sceneName = Mst.Args.AsString(Mst.Args.Names.RoomOnlineScene, SceneManager.GetActiveScene().name);

            logger.Info($"Loading server online scene... Scene: {sceneName}");

            ScenesLoader.LoadSceneByName(sceneName, null, () =>
            {
                logger.Info($"Room Server started and listening to: {serverIp}:{serverPort}");

                if (roomServerManager)
                    roomServerManager.StartServer();
            });
        }

        protected override void OnStoppedServer()
        {
            logger.Info("Room Server stopped");
            base.OnStoppedServer();
            if (roomServerManager) roomServerManager.StopServer();
        }

        protected override void OnPeerDisconnected(IPeer peer)
        {
            logger.Info($"Peer {peer.Id} disconnected");

            if (roomServerManager)
                roomServerManager.OnPeerDisconnected(peer.Id);
        }

        #region MESSAGE_HANDLERS

        protected virtual async Task ValidateRoomAccessRequestHandler(IIncomingMessage message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!roomServerManager)
            {
                message.RespondError(ResponseStatus.ServiceUnavailable, MstErrorCodes.ROOM_ACCESS_UNAVAILABLE);
                message.Peer.Disconnect("Room is invalid");
                return;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int completionState = 0;

            using (cancellationToken.Register(() =>
            {
                if (Interlocked.CompareExchange(ref completionState, 1, 0) == 0)
                    completion.TrySetCanceled();
            }))
            {
                roomServerManager.ValidateRoomAccess(message.Peer.Id, message.AsString(), (isSuccess, error) =>
                {
                    try
                    {
                        int state = Volatile.Read(ref completionState);

                        if (state == 0)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                if (Interlocked.CompareExchange(ref completionState, 1, 0) == 0)
                                    completion.TrySetCanceled();

                                return;
                            }

                            if (Interlocked.CompareExchange(ref completionState, 2, 0) != 0)
                                return;
                        }
                        else if (state != 2)
                        {
                            return;
                        }

                        if (!isSuccess)
                        {
                            logger.Error($"Unauthorized access to room was rejected. peerId={message.Peer.Id}");
                            message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.ROOM_ACCESS_DENIED);
                            message.Peer.Disconnect("Unauthorized access to room was rejected");
                            completion.TrySetResult(true);
                            return;
                        }

                        message.Respond(ResponseStatus.Success);
                        completion.TrySetResult(true);
                    }
                    catch (Exception exception)
                    {
                        completion.TrySetException(exception);
                    }
                }, cancellationToken,
                () => Interlocked.CompareExchange(ref completionState, 2, 0) == 0);

                await completion.Task;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        #endregion
    }
}
