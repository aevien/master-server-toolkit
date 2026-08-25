using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public interface IServer
    {
        /// <summary>
        /// Invoked, when a client connects to this socket
        /// </summary>
        event PeerActionHandler OnPeerConnectedEvent;

        /// <summary>
        /// Invoked, when client disconnects from this socket
        /// </summary>
        event PeerActionHandler OnPeerDisconnectedEvent;

        /// <summary>
        /// Adds a module to the server
        /// </summary>
        /// <param name="module"></param>
        /// <exception cref="System.InvalidOperationException">A server run is active.</exception>
        void AddModule(IBaseServerModule module);

        /// <summary>
        /// Adds a module and tries to initialize all of the uninitialized modules
        /// </summary>
        /// <param name="module"></param>
        /// <exception cref="System.InvalidOperationException">A server run is active.</exception>
        void AddModuleAndInitialize(IBaseServerModule module);

        /// <summary>
        /// Returns true, if this server contains a module of given type
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        bool ContainsModule(IBaseServerModule module);

        /// <summary>
        /// Tries to initialize modules that were not initialized,
        /// and returns true if all of the modules are initialized successfully
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">A server run is active.</exception>
        bool InitializeModules();

        /// <summary>
        /// Returns a module of specified type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetModule<T>() where T : class, IBaseServerModule;

        /// <summary>
        /// Returns an immutable list of initialized modules
        /// </summary>
        /// <returns></returns>
        List<IBaseServerModule> GetInitializedModules();

        /// <summary>
        /// Returns an immutable list of initialized modules
        /// </summary>
        /// <returns></returns>
        List<IBaseServerModule> GetUninitializedModules();

        /// <summary>
        /// Adds a message handler to the collection of handlers.
        /// It will be invoked when server receives a message with
        /// OpCode <see cref="IAsyncPacketHandler.OpCode"/>
        /// </summary>
        void RegisterMessageHandler(IAsyncPacketHandler handler);

        /// <summary>
        /// Adds a message handler to the collection of handlers.
        /// It will be invoked when server receives a message with
        /// OpCode <see cref="opCode"/>
        /// </summary>
        void RegisterMessageHandler(ushort opCode, AsyncIncommingMessageHandler handler);

        /// <summary>
        /// Adds a cancellation-aware message handler to the collection of handlers.
        /// The token is canceled when the current server run begins stopping.
        /// </summary>
        /// <param name="opCode">Operation code handled by the callback.</param>
        /// <param name="handler">Cancellation-aware handler.</param>
        void RegisterMessageHandler(ushort opCode, CancellableAsyncIncomingMessageHandler handler);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handler"></param>
        void RegisterMessageHandler(string opCode, AsyncIncommingMessageHandler handler);

        /// <summary>
        /// Adds a cancellation-aware message handler using a string operation code.
        /// </summary>
        /// <param name="opCode">String operation code.</param>
        /// <param name="handler">Cancellation-aware handler.</param>
        void RegisterMessageHandler(string opCode, CancellableAsyncIncomingMessageHandler handler);

        /// <summary>
        /// Returns a connected peer with a given ID
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        IPeer GetPeer(int peerId);

        /// <summary>
        /// Resolves a configured permission key to its numeric permission level.
        /// </summary>
        /// <param name="key">Permission key configured on the server.</param>
        /// <param name="permissionLevel">Resolved permission level when the key exists.</param>
        /// <returns><c>true</c> when the key exists; otherwise, <c>false</c>.</returns>
        bool TryGetPermissionLevel(string key, out int permissionLevel);
    }
}
