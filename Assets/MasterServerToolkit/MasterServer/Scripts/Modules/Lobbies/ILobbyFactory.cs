using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public interface ILobbyFactory
    {
        string Id { get; }
        ILobby CreateLobby(MstProperties options, IPeer creator);
    }
}