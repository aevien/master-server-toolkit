using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public delegate void JoinLobbyCallbackHandler(JoinedLobby lobby, string error);
    public delegate void JoinLobbyEventHandler(JoinedLobby lobby);
    public delegate void CreateLobbyCallbackHandler(int? lobbyId, string error);

    public class MstLobbiesClient : MstBaseClient
    {
        /// <summary>
        /// Invoked, when user joins a lobby
        /// </summary>
        public event JoinLobbyEventHandler OnJoinedLobbyEvent;

        /// <summary>
        /// Invoked, when user left a lobby
        /// </summary>
        public event Action OnLeftLobbyEvent;

        /// <summary>
        /// Instance of a lobby that was joined the last
        /// </summary>
        public JoinedLobby JoinedLobby { get; private set; }

        /// <summary>
        /// Check if client currently is in lobby
        /// </summary>
        public bool IsInLobby => JoinedLobby != null && !JoinedLobby.HasLeft;

        public MstLobbiesClient(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// Sends a request to create a lobby and joins it
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="properties"></param>
        /// <param name="callback"></param>
        public void CreateAndJoin(string factory, MstProperties options, JoinLobbyCallbackHandler callback)
        {
            CreateLobby(factory, options, (id, error) =>
            {
                if (!id.HasValue)
                {
                    callback.Invoke(null, "Failed to create lobby: " + error);
                    return;
                }

                JoinLobby(id.Value, (lobby, joinError) =>
                {
                    if (lobby == null)
                    {
                        callback.Invoke(null, "Failed to join the lobby: " + joinError);
                        return;
                    }

                    callback.Invoke(lobby, null);
                });
            });
        }

        /// <summary>
        /// Sends a request to create a lobby, using a specified factory
        /// </summary>
        public void CreateLobby(string factory, MstProperties options, CreateLobbyCallbackHandler calback)
        {
            CreateLobby(factory, options, calback, Connection);
        }

        /// <summary>
        /// Sends a request to create a lobby, using a specified factory
        /// </summary>
        public void CreateLobby(string factory, MstProperties options, CreateLobbyCallbackHandler calback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                calback.Invoke(null, "Not connected");
                return;
            }

            options.Set(MstDictKeys.LOBBY_FACTORY_ID, factory);

            connection.SendMessage(MstOpCodes.CreateLobby, options.ToBytes(), (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    calback.Invoke(null, response.AsString("Unknown error"));
                    return;
                }

                var lobbyId = response.AsInt();

                calback.Invoke(lobbyId, null);
            });
        }

        /// <summary>
        /// Sends a request to join a lobby
        /// </summary>
        /// <param name="lobbyId"></param>
        /// <param name="callback"></param>
        public void JoinLobby(int lobbyId, JoinLobbyCallbackHandler callback)
        {
            JoinLobby(lobbyId, callback, Connection);
        }

        /// <summary>
        /// Sends a request to join a lobby
        /// </summary>
        public void JoinLobby(int lobbyId, JoinLobbyCallbackHandler callback, IClientSocket connection)
        {
            // Send the message
            connection.SendMessage(MstOpCodes.JoinLobby, lobbyId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Unknown Error"));
                    return;
                }

                var data = response.AsPacket<LobbyDataPacket>();

                var joinedLobby = new JoinedLobby(data, connection);

                JoinedLobby = joinedLobby;

                callback?.Invoke(joinedLobby, null);
                OnJoinedLobbyEvent?.Invoke(joinedLobby);
            });
        }

        /// <summary>
        /// Sends a request to leave a lobby
        /// </summary>
        public void LeaveLobby(int lobbyId)
        {
            LeaveLobby(lobbyId, () => { }, Connection);
        }

        /// <summary>
        /// Sends a request to leave a lobby
        /// </summary>
        public void LeaveLobby(int lobbyId, Action callback)
        {
            LeaveLobby(lobbyId, callback, Connection);
        }

        /// <summary>
        /// Sends a request to leave a lobby
        /// </summary>
        public void LeaveLobby(int lobbyId, Action callback, IClientSocket connection)
        {
            connection.SendMessage(MstOpCodes.LeaveLobby, lobbyId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    Logs.Error(response.AsString("Something went wrong when trying to leave a lobby"));
                }

                callback?.Invoke();
                OnLeftLobbyEvent?.Invoke();
            });
        }

        /// <summary>
        /// Sets a ready status of current player
        /// </summary>
        /// <param name="isReady"></param>
        /// <param name="callback"></param>
        public void SetReadyStatus(bool isReady, SuccessCallback callback)
        {
            SetReadyStatus(isReady, callback, Connection);
        }

        /// <summary>
        /// Sets a ready status of current player
        /// </summary>
        public void SetReadyStatus(bool isReady, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected");
                return;
            }

            connection.SendMessage(MstOpCodes.SetLobbyAsReady, isReady ? 1 : 0, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sets lobby properties of a specified lobby id
        /// </summary>
        /// <param name="lobbyId"></param>
        /// <param name="properties"></param>
        /// <param name="callback"></param>
        public void SetLobbyProperties(int lobbyId, MstProperties properties, SuccessCallback callback)
        {
            SetLobbyProperties(lobbyId, properties, callback, Connection);
        }

        /// <summary>
        /// Sets lobby properties of a specified lobby id
        /// </summary>
        public void SetLobbyProperties(int lobbyId, MstProperties properties, SuccessCallback callback, IClientSocket connection)
        {
            var packet = new LobbyPropertiesSetPacket()
            {
                LobbyId = lobbyId,
                Properties = properties
            };

            connection.SendMessage(MstOpCodes.SetLobbyProperties, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Set's lobby user properties (current player sets his own properties,
        ///  which can be accessed by game server and etc.)
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="callback"></param>
        public void SetMyProperties(MstProperties properties, SuccessCallback callback)
        {
            SetMyProperties(properties, callback, Connection);
        }

        /// <summary>
        /// Set's lobby user properties (current player sets his own properties,
        ///  which can be accessed by game server and etc.)
        /// </summary>
        public void SetMyProperties(MstProperties properties, SuccessCallback callback, IClientSocket connection)
        {
            connection.SendMessage(MstOpCodes.SetMyProperties, properties.ToBytes(), Mst.Create.SuccessCallback(callback));
        }

        /// <summary>
        /// Current player sends a request to join a team
        /// </summary>
        /// <param name="lobbyId"></param>
        /// <param name="teamName"></param>
        /// <param name="callback"></param>
        public void JoinTeam(int lobbyId, string teamName, SuccessCallback callback)
        {
            JoinTeam(lobbyId, teamName, callback, Connection);
        }

        /// <summary>
        /// Current player sends a request to join a team
        /// </summary>
        public void JoinTeam(int lobbyId, string teamName, SuccessCallback callback, IClientSocket connection)
        {
            var packet = new LobbyJoinTeamPacket()
            {
                LobbyId = lobbyId,
                TeamName = teamName
            };

            connection.SendMessage(MstOpCodes.JoinLobbyTeam, packet, Mst.Create.SuccessCallback(callback));
        }

        /// <summary>
        /// Current player sends a chat message to lobby
        /// </summary>
        public void SendChatMessage(string message)
        {
            SendChatMessage(message, Connection);
        }

        /// <summary>
        /// Current player sends a chat message to lobby
        /// </summary>
        public void SendChatMessage(string message, IClientSocket connection)
        {
            connection.SendMessage(MstOpCodes.SendMessageToLobbyChat, message);
        }

        /// <summary>
        /// Sends a request to start a game
        /// </summary>
        public void StartGame(SuccessCallback callback, IClientSocket connection)
        {
            connection.SendMessage(MstOpCodes.StartLobbyGame, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a request to get access to room, which is assigned to this lobby
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="callback"></param>
        public void GetLobbyRoomAccess(MstProperties properties, RoomAccessCallback callback)
        {
            GetLobbyRoomAccess(properties, callback, Connection);
        }

        /// <summary>
        /// Sends a request to get access to room, which is assigned to this lobby
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void GetLobbyRoomAccess(MstProperties properties, RoomAccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(null, "Not connected");
                return;
            }

            connection.SendMessage(MstOpCodes.GetLobbyRoomAccess, properties.ToBytes(), (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Unknown Error"));
                    return;
                }

                var access = response.AsPacket<RoomAccessPacket>();

                Mst.Client.Rooms.TriggerAccessReceivedEvent(access);

                callback.Invoke(access, null);
            });
        }
    }
}