using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class RoomToMasterConnector : ConnectionHelper<RoomToMasterConnector>
    {
        protected override void Awake()
        {
            base.Awake();

            // If master IP is provided via cmd arguments
            serverIp = Mst.Args.AsString(Mst.Args.Names.MasterIp, serverIp);
            // If master port is provided via cmd arguments
            serverPort = Mst.Args.AsInt(Mst.Args.Names.MasterPort, serverPort);
        }

        protected override IClientSocket ConnectionFactory()
        {
            return Mst.Create.ClientSocket();
        }
    }
}