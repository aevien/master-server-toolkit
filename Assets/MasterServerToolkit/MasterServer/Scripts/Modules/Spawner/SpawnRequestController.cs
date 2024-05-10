using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnRequestController
    {
        /// <summary>
        /// Current connection
        /// </summary>
        private readonly IClientSocket connection;

        /// <summary>
        /// Current spawn id
        /// </summary>
        public int SpawnTaskId { get; private set; }

        /// <summary>
        /// Current spawn status
        /// </summary>
        public SpawnStatus Status { get; private set; } = SpawnStatus.None;

        /// <summary>
        /// A dictionary of options that user provided when requesting a 
        /// process to be spawned
        /// </summary>
        public MstProperties SpawnOptions { get; private set; }

        /// <summary>
        /// Fires when spawn status changed
        /// </summary>
        public event Action<SpawnStatus> OnStatusChangedEvent;

        /// <summary>
        /// Create new <see cref="SpawnRequestController"/> instance
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="connection"></param>
        /// <param name="spawnOptions"></param>
        public SpawnRequestController(int spawnId, IClientSocket connection, MstProperties spawnOptions)
        {
            this.connection = connection;
            SpawnTaskId = spawnId;
            SpawnOptions = spawnOptions;

            // Set handlers
            connection.RegisterMessageHandler(MstOpCodes.SpawnRequestStatusChange, StatusUpdateHandler);
        }

        /// <summary>
        /// Fires when new status received
        /// </summary>
        /// <param name="message"></param>
        private static void StatusUpdateHandler(IIncomingMessage message)
        {
            var data = message.AsPacket<SpawnStatusUpdatePacket>();

            Logs.Debug($"Status changed to {data.Status}");

            if (Mst.Client.Spawners.TryGetRequestController(data.SpawnId, out SpawnRequestController controller))
            {
                controller.Status = data.Status;
                controller.OnStatusChangedEvent?.Invoke(data.Status);
            }
        }

        /// <summary>
        /// Abort current spawn process by Id
        /// </summary>
        public void Abort()
        {
            Mst.Client.Spawners.AbortSpawn(SpawnTaskId);
        }

        /// <summary>
        /// Abort current spawn process by Id
        /// </summary>
        /// <param name="handler"></param>
        public void Abort(SuccessCallback handler)
        {
            Mst.Client.Spawners.AbortSpawn(SpawnTaskId, handler);
        }

        /// <summary>
        /// Retrieves data, which was given to master server by a spawned process,
        /// which was finalized
        /// </summary>
        public void GetFinalizationData(MstSpawnersClient.FinalizationDataResultHandler handler)
        {
            Mst.Client.Spawners.GetFinalizationData(SpawnTaskId, handler, connection);
        }
    }
}