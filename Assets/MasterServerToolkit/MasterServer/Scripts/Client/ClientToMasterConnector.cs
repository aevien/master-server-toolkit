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
            if (Mst.Args.IsProvided(Mst.Args.Names.MasterIp))
            {
                serverIp = Mst.Args.MasterIp;
            }

            // If master port is provided via cmd arguments
            if (Mst.Args.IsProvided(Mst.Args.Names.MasterPort))
            {
                serverPort = Mst.Args.MasterPort;
            }
        }
    }
}