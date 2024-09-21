using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class LobbiesModule : BaseServerModule, IGamesProvider
    {
        #region INSPECTOR

        [Header("Configuration")]
        public int createLobbiesPermissionLevel = 0;
        [Tooltip("If true, don't allow player to create a lobby if he has already joined one")]
        public bool dontAllowCreatingIfJoined = true;
        [Tooltip("How many lobbies can a user join concurrently")]
        public int joinedLobbiesLimit = 1;

        #endregion

        /// <summary>
        /// Next lobby Id
        /// </summary>
        private int nextLobbyId;

        /// <summary>
        /// Lobby factories list
        /// </summary>
        protected Dictionary<string, ILobbyFactory> factories;

        /// <summary>
        /// Lobbies list
        /// </summary>
        protected Dictionary<int, ILobby> lobbies;

        public SpawnersModule SpawnersModule { get; protected set; }
        public RoomsModule RoomsModule { get; protected set; }

        protected override void Awake()
        {
            base.Awake();

            AddOptionalDependency<SpawnersModule>();
            AddOptionalDependency<RoomsModule>();
        }

        public override void Initialize(IServer server)
        {
            // Get dependencies
            SpawnersModule = server.GetModule<SpawnersModule>();
            RoomsModule = server.GetModule<RoomsModule>();

            factories = factories ?? new Dictionary<string, ILobbyFactory>();
            lobbies = lobbies ?? new Dictionary<int, ILobby>();

            server.RegisterMessageHandler(MstOpCodes.CreateLobby, CreateLobbyHandle);
            server.RegisterMessageHandler(MstOpCodes.JoinLobby, JoinLobbyHandler);
            server.RegisterMessageHandler(MstOpCodes.LeaveLobby, LeaveLobbyHandler);
            server.RegisterMessageHandler(MstOpCodes.SetLobbyProperties, SetLobbyPropertiesMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.SetMyProperties, SetMyPropertiesMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.JoinLobbyTeam, JoinLobbyTeamMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.SendMessageToLobbyChat, SendMessageToLobbyChatMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.SetLobbyAsReady, SetLobbyAsReadyMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.StartLobbyGame, StartLobbyGameMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.GetLobbyRoomAccess, GetLobbyRoomAccessMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.GetLobbyMemberData, GetLobbyMemberDataMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.GetLobbyInfo, GetLobbyInfoMessageHandler);
        }

        /// <summary>
        /// Checks if peer has permission to create lobby
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        protected virtual bool HasPermissionToCreate(IPeer peer)
        {
            var extension = peer.GetExtension<SecurityInfoPeerExtension>();
            return extension != null && extension.PermissionLevel >= createLobbiesPermissionLevel;
        }

        /// <summary>
        /// Create new unique lobby Id
        /// </summary>
        /// <returns></returns>
        public int NextLobbyId()
        {
            return nextLobbyId++;
        }

        /// <summary>
        /// Add new lobby factory to list
        /// </summary>
        /// <param name="factory"></param>
        public void AddFactory(ILobbyFactory factory)
        {
            // In case the module has not been initialized yet
            if (factories == null)
            {
                factories = new Dictionary<string, ILobbyFactory>();
            }

            if (factories.ContainsKey(factory.Id))
            {
                logger.Warn("You are overriding a factory with same id");
            }

            factories[factory.Id] = factory;
        }

        /// <summary>
        /// Adds new lobby to list of lobbies
        /// </summary>
        /// <param name="lobby"></param>
        /// <returns></returns>
        public bool AddLobby(ILobby lobby)
        {
            if (lobby == null) return false;

            if (lobbies.ContainsKey(lobby.Id))
            {
                logger.Error("Failed to add a lobby - lobby with same id already exists");
                return false;
            }

            lobbies.Add(lobby.Id, lobby);

            lobby.OnDestroyedEvent += OnLobbyDestroyedEventHandler;

            return true;
        }

        /// <summary>
        /// Invoked, when lobby is destroyed
        /// </summary>
        /// <param name="lobby"></param>
        protected virtual void OnLobbyDestroyedEventHandler(ILobby lobby)
        {
            lobbies.Remove(lobby.Id);
            lobby.OnDestroyedEvent -= OnLobbyDestroyedEventHandler;
        }


        /// <summary>
        /// Get or create lobby extension for the peer
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        protected virtual LobbyUserPeerExtension GetOrCreateLobbyUserPeerExtension(IPeer peer)
        {
            var extension = peer.GetExtension<LobbyUserPeerExtension>();

            if (extension == null)
            {
                extension = new LobbyUserPeerExtension(peer);
                peer.AddExtension(extension);
            }

            return extension;
        }

        #region INCOMING MESSAGES HANDLERS

        protected virtual Task CreateLobbyHandle(IIncomingMessage message)
        {
            try
            {
                // We may need to check permission of requester
                if (!HasPermissionToCreate(message.Peer))
                {
                    message.Respond("Insufficient permissions", ResponseStatus.Unauthorized);
                    return Task.CompletedTask;
                }

                // Let's get or create new lobby user peer extension
                var lobbyUser = GetOrCreateLobbyUserPeerExtension(message.Peer);

                // If peer is already in a lobby and system does not allow to create if user is joined
                if (dontAllowCreatingIfJoined && lobbyUser.CurrentLobby != null)
                {
                    message.Respond("You are already in a lobby", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                // Deserialize properties of the lobby
                var options = MstProperties.FromBytes(message.AsBytes());

                // Get lobby factory Id or empty string
                string lobbyFactoryId = options.AsString(MstDictKeys.LOBBY_FACTORY_ID);

                if (string.IsNullOrEmpty(lobbyFactoryId))
                {
                    message.Respond("Invalid request (undefined factory)", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                // Get the lobby factory
                factories.TryGetValue(lobbyFactoryId, out ILobbyFactory factory);

                if (factory == null)
                {
                    message.Respond("Unavailable lobby factory", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                var newLobby = factory.CreateLobby(options, message.Peer);

                if (!AddLobby(newLobby))
                {
                    message.Respond("Lobby registration failed", ResponseStatus.Error);
                    return Task.CompletedTask;
                }

                logger.Info("Lobby created: " + newLobby.Id);

                // Respond with success and lobby id
                message.Respond(newLobby.Id, ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// Handles a request from user to join a lobby
        /// </summary>
        /// <param name="message"></param>
        protected virtual Task JoinLobbyHandler(IIncomingMessage message)
        {
            try
            {
                var lobbyUser = GetOrCreateLobbyUserPeerExtension(message.Peer);

                if (lobbyUser.CurrentLobby != null)
                {
                    message.Respond("You're already in a lobby", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                var lobbyId = message.AsInt();

                lobbies.TryGetValue(lobbyId, out ILobby lobby);

                if (lobby == null)
                {
                    message.Respond("Lobby was not found", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                if (!lobby.AddPlayer(lobbyUser, out string error))
                {
                    message.Respond(error ?? "Failed to add player to lobby", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                var data = lobby.GenerateLobbyData(lobbyUser);

                message.Respond(data, ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// Handles a request from user to leave a lobby
        /// </summary>
        /// <param name="message"></param>
        protected virtual Task LeaveLobbyHandler(IIncomingMessage message)
        {
            try
            {
                var lobbyId = message.AsInt();

                lobbies.TryGetValue(lobbyId, out ILobby lobby);

                var lobbiesExt = GetOrCreateLobbyUserPeerExtension(message.Peer);

                if (lobby != null)
                {
                    lobby.RemovePlayer(lobbiesExt);
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual Task SetLobbyPropertiesMessageHandler(IIncomingMessage message)
        {
            try
            {
                var data = message.AsPacket<LobbyPropertiesSetPacket>();

                lobbies.TryGetValue(data.LobbyId, out ILobby lobby);

                if (lobby == null)
                {
                    message.Respond("Lobby was not found", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                var lobbiesExt = GetOrCreateLobbyUserPeerExtension(message.Peer);

                foreach (var dataProperty in data.Properties.ToDictionary())
                {
                    if (!lobby.SetProperty(lobbiesExt, dataProperty.Key, dataProperty.Value))
                    {
                        message.Respond("Failed to set the property: " + dataProperty.Key,
                            ResponseStatus.Failed);

                        return Task.CompletedTask;
                    }
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private Task SetMyPropertiesMessageHandler(IIncomingMessage message)
        {
            try
            {
                // Get lobby user peer extension
                var lobbyUser = GetOrCreateLobbyUserPeerExtension(message.Peer);

                // Get current lobby this user joined in
                var lobby = lobbyUser.CurrentLobby;

                if (lobby == null)
                {
                    message.Respond("Lobby was not found", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                // Properties to be changed
                var properties = new Dictionary<string, string>().FromBytes(message.AsBytes());

                // Get member of lobby by its lobby user extension
                var member = lobby.GetMemberByExtension(lobbyUser);

                foreach (var dataProperty in properties)
                {
                    // We don't change properties directly,
                    // because we want to allow an implementation of lobby
                    // to do "sanity" checking
                    if (!lobby.SetPlayerProperty(member, dataProperty.Key, dataProperty.Value))
                    {
                        message.Respond("Failed to set property: " + dataProperty.Key, ResponseStatus.Failed);
                        return Task.CompletedTask;
                    }
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual Task SetLobbyAsReadyMessageHandler(IIncomingMessage message)
        {
            try
            {
                var isReady = message.AsInt() > 0;

                var lobbiesExt = GetOrCreateLobbyUserPeerExtension(message.Peer);
                var lobby = lobbiesExt.CurrentLobby;

                if (lobby == null)
                {
                    message.Respond("You're not in a lobby", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                var member = lobby.GetMemberByExtension(lobbiesExt);

                if (member == null)
                {
                    message.Respond("Invalid request", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                lobby.SetReadyState(member, isReady);
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual Task JoinLobbyTeamMessageHandler(IIncomingMessage message)
        {
            try
            {
                var data = message.AsPacket<LobbyJoinTeamPacket>();

                var lobbiesExt = GetOrCreateLobbyUserPeerExtension(message.Peer);
                var lobby = lobbiesExt.CurrentLobby;

                if (lobby == null)
                {
                    message.Respond("You're not in a lobby", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                var player = lobby.GetMemberByExtension(lobbiesExt);

                if (player == null)
                {
                    message.Respond("Invalid request", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                if (!lobby.TryJoinTeam(data.TeamName, player))
                {
                    message.Respond("Failed to join a team: " + data.TeamName, ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual Task SendMessageToLobbyChatMessageHandler(IIncomingMessage message)
        {
            try
            {
                var lobbiesExt = GetOrCreateLobbyUserPeerExtension(message.Peer);
                var lobby = lobbiesExt.CurrentLobby;

                var member = lobby.GetMemberByExtension(lobbiesExt);

                // Invalid request
                if (member == null)
                {
                    return Task.CompletedTask;
                }

                lobby.ChatMessageHandler(member, message);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual Task StartLobbyGameMessageHandler(IIncomingMessage message)
        {
            try
            {
                var lobbiesExt = GetOrCreateLobbyUserPeerExtension(message.Peer);
                var lobby = lobbiesExt.CurrentLobby;

                if (!lobby.StartGameManually(lobbiesExt))
                {
                    message.Respond("Failed starting the game", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual Task GetLobbyRoomAccessMessageHandler(IIncomingMessage message)
        {
            try
            {
                var lobbiesExt = GetOrCreateLobbyUserPeerExtension(message.Peer);
                var lobby = lobbiesExt.CurrentLobby;

                lobby.GameAccessRequestHandler(message);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual Task GetLobbyMemberDataMessageHandler(IIncomingMessage message)
        {
            try
            {
                var data = message.AsPacket<IntPairPacket>();
                var lobbyId = data.A;
                var peerId = data.B;

                lobbies.TryGetValue(lobbyId, out ILobby lobby);

                if (lobby == null)
                {
                    message.Respond("Lobby not found", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                var member = lobby.GetMemberByPeerId(peerId);

                if (member == null)
                {
                    message.Respond("Player is not in the lobby", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                message.Respond(member.GenerateDataPacket(), ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual Task GetLobbyInfoMessageHandler(IIncomingMessage message)
        {
            try
            {
                var lobbyId = message.AsInt();

                lobbies.TryGetValue(lobbyId, out ILobby lobby);

                if (lobby == null)
                {
                    message.Respond("Lobby not found", ResponseStatus.Failed);
                    return Task.CompletedTask;
                }

                message.Respond(lobby.GenerateLobbyData(), ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        #endregion

        public virtual IEnumerable<GameInfoPacket> GetPublicGames(IPeer peer, MstProperties filters)
        {
            var lobbiesList = filters != null && filters.Has(MstDictKeys.ROOM_ID) ? lobbies.Values.Where(l => l.Id == filters.AsInt(MstDictKeys.ROOM_ID)) : lobbies.Values;
            var games = new List<GameInfoPacket>();

            foreach (var lobby in lobbiesList)
            {
                var game = new GameInfoPacket
                {
                    Id = lobby.Id,
                    Address = lobby.GameIp + ":" + lobby.GamePort,
                    IsPasswordProtected = false,
                    MaxPlayers = lobby.MaxPlayers,
                    Name = lobby.Name,
                    OnlinePlayers = lobby.PlayerCount,
                    Properties = GetPublicLobbyProperties(peer, lobby, filters),
                    Type = GameInfoType.Lobby
                };

                game.OnlinePlayersList = lobby.Members.Select(m => m.Username).ToList();
                games.Add(game);
            }

            return games;
        }

        public virtual MstProperties GetPublicLobbyProperties(IPeer peer, ILobby lobby, MstProperties playerFilters)
        {
            return lobby.GetPublicProperties(peer);
        }
    }
}