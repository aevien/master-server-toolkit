using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class LobbiesServer : MstBaseClient
    {
        public delegate void LobbyMemberDataCallback(LobbyMemberData memberData, string error);
        public delegate void LobbyInfoCallback(LobbyDataPacket info, string error);

        public LobbiesServer(IClientSocket connection) : base(connection)
        {
            LobbiesClient.RegisterErrorParsers();
        }

        /// <summary>
        /// Retrieves lobby member data of user, who has connected to master server with
        /// a specified peerId
        /// </summary>
        /// <param name="lobbyId"></param>
        /// <param name="peerId"></param>
        /// <param name="callback"></param>
        public void GetMemberData(int lobbyId, int peerId, LobbyMemberDataCallback callback = null)
        {
            GetMemberData(lobbyId, peerId, callback, Connection);
        }

        /// <summary>
        /// Retrieves lobby member data of user, who has connected to master server with
        /// a specified peerId
        /// </summary>
        public void GetMemberData(int lobbyId, int peerId, LobbyMemberDataCallback callback, IClientSocket connection)
        {
            var packet = new IntPairPacket
            {
                A = lobbyId,
                B = peerId
            };

            connection.SendMessage(MstOpCodes.GetLobbyMemberData, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(status, response));
                    return;
                }

                var memberData = response.AsPacket<LobbyMemberData>();
                callback?.Invoke(memberData, null);
            });
        }

        /// <summary>
        /// Retrieves information about the lobby
        /// </summary>
        public void GetLobbyInfo(int lobbyId, LobbyInfoCallback callback = null)
        {
            GetLobbyInfo(lobbyId, callback, Connection);
        }

        /// <summary>
        /// Retrieves information about the lobby
        /// </summary>
        public void GetLobbyInfo(int lobbyId, LobbyInfoCallback callback, IClientSocket connection)
        {
            connection.SendMessage(MstOpCodes.GetLobbyInfo, lobbyId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(status, response));
                    return;
                }

                var memberData = response.AsPacket<LobbyDataPacket>();
                callback?.Invoke(memberData, null);
            });
        }
    }
}
