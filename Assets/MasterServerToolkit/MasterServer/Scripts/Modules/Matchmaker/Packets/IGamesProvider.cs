using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public interface IGamesProvider
    {
        IEnumerable<GameInfoPacket> GetPublicGames(IPeer peer, MstProperties filters);
    }
}