using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public interface ISpawnerController : IDisposable
    {
        event Action OnProcessStartedEvent;
        event Action OnProcessKilledEvent;
        SpawnerConfig SpawnSettings { get; }
        Logger Logger { get; }
        IClientSocket Connection { get; }
        int SpawnerId { get; }

        /// <summary>
        /// Requests removal of this controller's registration from the master server.
        /// </summary>
        /// <param name="callback">Receives whether the master server accepted the request.</param>
        void RequestUnregister(SuccessCallback callback = null);
        void SpawnRequestHandler(SpawnRequestPacket data, SuccessCallback callback);
        ResponseStatus KillRequestHandler(int spawnId);
        void KillProcesses();
        int ProcessesCount();
    }
}
