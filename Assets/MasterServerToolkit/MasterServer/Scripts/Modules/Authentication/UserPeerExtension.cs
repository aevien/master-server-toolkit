using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Instance of this class will be added to 
    /// extensions of a peer who has logged in 
    /// </summary>
    public class UserPeerExtension : IUserPeerExtension
    {
        public IPeer Peer { get; private set; }
        public string UserId => Account.Id;
        public string Username => Account.Username;

        public IAccountInfoData Account { get; set; }

        public UserPeerExtension(IPeer peer)
        {
            Peer = peer;
        }

        public AccountInfoPacket CreateAccountInfoPacket()
        {
            return new AccountInfoPacket(Account);
        }
    }
}