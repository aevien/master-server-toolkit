﻿using MasterServerToolkit.Logging;
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
        void SpawnRequestHandler(SpawnRequestPacket data, SuccessCallback callback);
        void KillRequestHandler(int spawnId);
        void KillProcesses();
        int ProcessesCount();
    }
}