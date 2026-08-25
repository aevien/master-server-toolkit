using MasterServerToolkit.Networking;
using MasterServerToolkit.Logging;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class AuthServer : MstBaseClient
    {
        public delegate void RoomUserAccountInfoCallback(RoomUserAccountInfoPacket accountInfo, string error);

        public AuthServer(IClientSocket connection) : base(connection)
        {
            RegisterErrors(
                MstErrorCodes.AUTH_PERMISSION_DENIED,
                MstErrorCodes.USER_IS_NOT_LOGGED_IN);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="callback"></param>
        public void GetAccountInfoByUsername(string username, RoomUserAccountInfoCallback callback = null)
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
                callback?.Invoke(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.GetAccountInfoByUsername, username, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(status, response));
                    return;
                }

                RoomUserAccountInfoPacket data;

                try
                {
                    data = response.AsPacket<RoomUserAccountInfoPacket>();
                }
                catch (Exception exception)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(ResponseStatus.Error));
                    Logs.Error($"Failed to decode account info response for username '{username}': {exception.Message}", LogChannels.Security);
                    return;
                }

                if (data == null)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(ResponseStatus.Error));
                    Logs.Error($"Failed to decode account info response for username '{username}': packet is missing", LogChannels.Security);
                    return;
                }

                callback?.Invoke(data, null);
            });
        }

        /// <summary>
        /// Gets account information of a client, who is connected to master server, 
        /// and who's peer id matches the one provided
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="callback"></param>
        public void GetAccountInfoByPeer(int peerId, RoomUserAccountInfoCallback callback = null)
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
                callback?.Invoke(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.GetAccountInfoByPeer, peerId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(status, response));
                    return;
                }

                RoomUserAccountInfoPacket data;

                try
                {
                    data = response.AsPacket<RoomUserAccountInfoPacket>();
                }
                catch (Exception exception)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(ResponseStatus.Error));
                    Logs.Error($"Failed to decode account info response for peer {peerId}: {exception.Message}", LogChannels.Security);
                    return;
                }

                if (data == null)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(ResponseStatus.Error));
                    Logs.Error($"Failed to decode account info response for peer {peerId}: packet is missing", LogChannels.Security);
                    return;
                }

                callback?.Invoke(data, null);
            });
        }

    }
}
