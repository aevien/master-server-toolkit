using MasterServerToolkit.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public interface IBaseServerModule
    {
        string Id {  get; }
        List<Type> Dependencies { get; }
        List<Type> OptionalDependencies { get; }
        ServerBehaviour Server { get; set; }
        void Initialize(IServer server);
        MstJson Info();
        MstJson Details();
    }

    /// <summary>
    /// Validates module configuration before the server opens its listening socket.
    /// </summary>
    public interface IServerStartupValidator
    {
        /// <summary>
        /// Throws an exception when the current configuration must prevent server startup.
        /// </summary>
        void ValidateServerStartup();
    }

    /// <summary>
    /// Defines work that belongs to one start/stop run of a server transport.
    /// </summary>
    /// <remarks>
    /// <see cref="IBaseServerModule.Initialize"/> configures a module once, while these methods are
    /// invoked for every server restart. Unity-owned timers and coroutines must be started and stopped
    /// here so work from an old run cannot mutate a later run.
    /// </remarks>
    public interface IServerRunModule
    {
        /// <summary>
        /// Starts work owned by the current server run.
        /// </summary>
        /// <param name="runCancellationToken">Token canceled as soon as the current run begins stopping.</param>
        void StartServerRun(CancellationToken runCancellationToken);

        /// <summary>
        /// Stops work owned by the current server run and flushes any required state.
        /// </summary>
        /// <returns>A task that completes when the module no longer owns work from the stopped run.</returns>
        Task StopServerRunAsync();
    }
}
