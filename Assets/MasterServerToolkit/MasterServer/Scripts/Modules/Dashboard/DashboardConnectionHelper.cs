using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class DashboardConnectionHelper : ConnectionHelper<DashboardConnectionHelper>
    {
        protected override void Awake()
        {
            base.Awake();

            // If IP is provided via cmd arguments
            serverIp = Mst.Args.AsString(Mst.Args.Names.DashboardIp, serverIp);
            // If port is provided via cmd arguments
            serverPort = Mst.Args.AsInt(Mst.Args.Names.DashboardPort, serverPort);
        }

        protected override IClientSocket ConnectionFactory()
        {
            return Mst.Create.ClientSocket();
        }
    }
}