using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class MstAuthServer : MstBaseClient
    {
        public delegate void PeerAccountInfoCallback(PeerAccountInfoPacket accountInfo, string accountError);

        public MstAuthServer(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// Gets account information of a client, who is connected to master server, 
        /// and who's peer id matches the one provided
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="callback"></param>
        public void GetPeerAccountInfo(int peerId, PeerAccountInfoCallback callback)
        {
            GetPeerAccountInfo(peerId, callback, Connection);
        }

        /// <summary>
        /// Gets account information of a client, who is connected to master server, 
        /// and who's peer id matches the one provided
        /// </summary>
        public void GetPeerAccountInfo(int peerId, PeerAccountInfoCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(null, "Not connected to server");
                return;
            }

            connection.SendMessage(MstOpCodes.GetPeerAccountInfo, peerId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Unknown error"));
                    return;
                }

                var data = response.AsPacket(new PeerAccountInfoPacket());

                callback.Invoke(data, null);
            });
        }
    }
}