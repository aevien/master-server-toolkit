namespace MasterServerToolkit.MasterServer
{
    public interface IRoomPlayerPeerExtension : Networking.IPeerExtension
    {
        /// <summary>
        /// Peer Id that master server gives to client
        /// </summary>
        int MasterPeerId { get; }

        /// <summary>
        /// Username that client has
        /// </summary>
        string Username { get; }

        /// <summary>
        /// Properties of client account
        /// </summary>
        MstProperties Properties { get; }
    }
}
