namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// This is an interface of a user extension.
    /// Implementation of this interface will be stored in peer's extensions
    /// after he logs in
    /// </summary>
    public interface IUserPeerExtension : IPeerExtension
    {
        string Username { get; }
        IAccountInfoData Account { get; set; }
        AccountInfoPacket CreateAccountInfoPacket();
    }
}