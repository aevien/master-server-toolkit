using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// This is an interface of a user extension.
    /// Implementation of this interface will be stored in peer's extensions
    /// after he logs in
    /// </summary>
    public interface IUserPeerExtension : IPeerExtension
    {
        /// <summary>
        /// Current peer userid
        /// </summary>
        string UserId { get; }
        /// <summary>
        /// Current peer login
        /// </summary>
        string Username { get; }
        /// <summary>
        /// Current peer account 
        /// </summary>
        IAccountInfoData Account { get; set; }
        /// <summary>
        /// Id of the room the current user joined
        /// </summary>
        int JoinedRoomID { get; set; }
        /// <summary>
        /// Create account info message to sent to peer
        /// </summary>
        /// <returns></returns>
        AccountInfoPacket CreateAccountInfoPacket();
        /// <summary>
        /// Check if current peer is owned by user and it is joined room
        /// </summary>
        /// <returns></returns>
        bool HasJoinedRoom();
    }
}