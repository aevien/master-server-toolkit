using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class MstAuthServer : MstBaseClient
    {
        public delegate void RoomUserAccountInfoCallback(RoomUserAccountInfoPacket accountInfo, string error);

        public MstAuthServer(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="callback"></param>
        public void GetAccountInfoByUsername(string username, RoomUserAccountInfoCallback callback)
        {
            GetAccountInfoByUsername(username, callback, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void GetAccountInfoByUsername(string username, RoomUserAccountInfoCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(null, "Not connected to server");
                return;
            }

            connection.SendMessage(MstOpCodes.GetAccountInfoByUsername, username, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Unknown error"));
                    return;
                }

                var data = response.AsPacket<RoomUserAccountInfoPacket>();
                callback.Invoke(data, null);
            });
        }

        /// <summary>
        /// Gets account information of a client, who is connected to master server, 
        /// and who's peer id matches the one provided
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="callback"></param>
        public void GetAccountInfoByPeer(int peerId, RoomUserAccountInfoCallback callback)
        {
            GetAccountInfoByPeer(peerId, callback, Connection);
        }

        /// <summary>
        /// Gets account information of a client, who is connected to master server, 
        /// and who's peer id matches the one provided
        /// </summary>
        public void GetAccountInfoByPeer(int peerId, RoomUserAccountInfoCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(null, "Not connected to server");
                return;
            }

            connection.SendMessage(MstOpCodes.GetAccountInfoByPeer, peerId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Unknown error"));
                    return;
                }

                var data = response.AsPacket<RoomUserAccountInfoPacket>();

                callback.Invoke(data, null);
            });
        }
    }
}