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
        void AddModule(IBaseServerModule module);

        /// <summary>
        /// Adds a module and tries to initialize all of the uninitialized modules
        /// </summary>
        /// <param name="module"></param>
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
        /// OpCode <see cref="IPacketHandler.OpCode"/>
        /// </summary>
        void RegisterMessageHandler(IPacketHandler handler);

        /// <summary>
        /// Adds a message handler to the collection of handlers.
        /// It will be invoked when server receives a message with
        /// OpCode <see cref="opCode"/>
        /// </summary>
        void RegisterMessageHandler(ushort opCode, IncommingMessageHandler handler);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handler"></param>
        void RegisterMessageHandler(string opCode, IncommingMessageHandler handler);

        /// <summary>
        /// Returns a connected peer with a given ID
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        IPeer GetPeer(int peerId);
    }
}