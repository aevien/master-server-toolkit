using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnTaskController
    {
        private readonly IClientSocket _connection;
        public int SpawnId { get; private set; }
        public MstProperties Options { get; private set; }

        public SpawnTaskController(int spawnId, MstProperties options, IClientSocket connection)
        {
            _connection = connection;
            SpawnId = spawnId;
            Options = options;
        }

        /// <summary>
        /// Finalize spawn task
        /// </summary>
        public void FinalizeTask()
        {
            FinalizeTask(new MstProperties());
        }

        /// <summary>
        /// Finalize spawn task
        /// </summary>
        /// <param name="finalizationData"></param>
        public void FinalizeTask(MstProperties finalizationData)
        {
            FinalizeTask(finalizationData, () => { });
        }

        /// <summary>
        /// Finalize spawn task
        /// </summary>
        /// <param name="finalizationData"></param>
        /// <param name="callback"></param>
        public void FinalizeTask(MstProperties finalizationData, Action callback)
        {
            Mst.Server.Spawners.FinalizeSpawnedProcess(SpawnId, finalizationData, (successful, error) =>
            {
                if (error != null)
                {
                    Logs.Error("Error while completing the spawn task: " + error);
                }

                callback.Invoke();
            }, _connection);
        }
    }
}