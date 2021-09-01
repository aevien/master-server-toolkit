using UnityEngine.Networking;

namespace MasterServerToolkit.Networking.Unet
{
    public class UnetSocketTopology
    {
        static UnetSocketTopology()
        {
            var config = new ConnectionConfig
            {
                DisconnectTimeout = 60 * 1000,
                UdpSocketReceiveBufferMaxSize = 0xFFFF
            };

            ReliableFragmented = config.AddChannel(QosType.ReliableFragmented);
            ReliableFragmentedSequenced = config.AddChannel(QosType.ReliableFragmentedSequenced);
            Unreliable = config.AddChannel(QosType.Unreliable);

            Topology = new HostTopology(config, 5000);
        }

        public static HostTopology Topology { get; private set; }
        public static int ReliableFragmented { get; private set; }
        public static int Unreliable { get; private set; }
        public static int ReliableFragmentedSequenced { get; private set; }
    }
}