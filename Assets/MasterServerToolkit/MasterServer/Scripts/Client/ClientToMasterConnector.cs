using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Automatically connects to master server
    /// </summary>
    [AddComponentMenu("Master Server Toolkit/ClientToMasterConnector")]
    public class ClientToMasterConnector : ConnectionHelper<ClientToMasterConnector>
    {
        protected override void Awake()
        {
            base.Awake();

            // If master IP is provided via cmd arguments
            serverIP = Mst.Args.AsString(Mst.Args.Names.MasterIp, serverIP);
            // If master port is provided via cmd arguments
            serverPort = Mst.Args.AsInt(Mst.Args.Names.MasterPort, serverPort);
        }
    }
}