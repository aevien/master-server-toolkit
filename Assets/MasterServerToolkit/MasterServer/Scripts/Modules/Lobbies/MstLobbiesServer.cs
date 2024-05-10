using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class MstLobbiesServer : MstBaseClient
    {
        public delegate void LobbyMemberDataCallback(LobbyMemberData memberData, string error);
        public delegate void LobbyInfoCallback(LobbyDataPacket info, string error);

        public MstLobbiesServer(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// Retrieves lobby member data of user, who has connected to master server with
        /// a specified peerId
        /// </summary>
        /// <param name="lobbyId"></param>
        /// <param name="peerId"></param>
        /// <param name="callback"></param>
        public void GetMemberData(int lobbyId, int peerId, LobbyMemberDataCallback callback)
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
                    callback.Invoke(null, response.AsString("Unknown error"));
                    return;
                }

                var memberData = response.AsPacket<LobbyMemberData>();
                callback.Invoke(memberData, null);
            });
        }

        /// <summary>
        /// Retrieves information about the lobby
        /// </summary>
        public void GetLobbyInfo(int lobbyId, LobbyInfoCallback callback)
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
                    callback.Invoke(null, response.AsString("Unknown error"));
                    return;
                }

                var memberData = response.AsPacket<LobbyDataPacket>();
                callback.Invoke(memberData, null);
            });
        }
    }
}