namespace MasterServerToolkit.Networking
{
    public interface IPeerExtension
    {
        /// <summary>
        /// Peer of connected client
        /// </summary>
        IPeer Peer { get; }
    }
}