using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class RemoteConfigModuleServer : RemoteConfigModuleClient
    {
        public RemoteConfigModuleServer(IClientSocket connection)
            : base(connection) { }
    }
}
