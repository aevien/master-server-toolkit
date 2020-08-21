using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public interface IPeerExtension
    {
        IPeer Peer { get; }
    }
}