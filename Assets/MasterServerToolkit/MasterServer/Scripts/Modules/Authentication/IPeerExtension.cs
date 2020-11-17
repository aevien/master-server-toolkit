using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public interface IPeerExtension
    {
        /// <summary>
        /// Peer of connected client
        /// </summary>
        IPeer Peer { get; }
    }
}