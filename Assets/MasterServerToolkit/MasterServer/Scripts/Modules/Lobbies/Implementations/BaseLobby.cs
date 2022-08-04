using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class BaseLobby : ILobby
    {
        /// <summary>
        /// Current state of the lobby
        /// </summary>
        private LobbyState _state;

        /// <summary>
        /// Status infoof the lobby
        /// </summary>
        private string _statusText = "";

        /// <summary>
        /// 
        /// </summary>
        private LobbyMember _gameMaster;

        /// <summary>
        /// Lobby teams list
        /// </summary>
        protected Dictionary<string, LobbyTeam> teamsList;

        /// <summary>
        /// Lobby subscribers list
        /// </summary>
        protected HashSet<IPeer> subscribersList;

        /// <summary>
        /// Filtered list of lobby members by peer Id
        /// </summary>
        protected Dictionary<int, LobbyMember> membersByPeerIdList;

        /// <summary>
        /// Filtered list of lobby members by username
        /// </summary>
        protected Dictionary<string, LobbyMember> membersByUsernameList;

        /// <summary>
        /// Lobby properties
        /// </summary>
        protected MstProperties propertiesList;

        /// <summary>
        /// 
        /// </summary>
        protected List<LobbyPropertyData> controls;

        /// <summary>
        /// 
        /// </summary>
        protected SpawnTask gameSpawnTask;

        /// <summary>
        /// 
        /// </summary>
        protected RegisteredRoom lobbyRoom;

        /// <summary>
        /// 
        /// </summary>
        protected LobbiesModule Module { get; private set; }

        /// <summary>
        /// List of all members
        /// </summary>
        public List<LobbyMember> Members => membersByPeerIdList.Values.ToList();

        /// <summary>
        /// When new player added to lobby
        /// </summary>
        public event Action<LobbyMember> OnPlayerAddedEvent;

        /// <summary>
        /// When one of the players removed from lobby
        /// </summary>
        public event Action<LobbyMember> OnPlayerRemovedEvent;

        /// <summary>
        /// When lobby is destroyed
        /// </summary>
        public event Action<ILobby> OnDestroyedEvent;

        /// <summary>
        /// Logger of the lobby
        /// </summary>
        public Logger Logger { get; protected set; }

        /// <summary>
        /// Id of the lobby
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// Name of the lobby 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The number of players in lobby
        /// </summary>
        public int PlayerCount { get { return membersByUsernameList.Count; } }

        /// <summary>
        /// Check if lobby is destroyed
        /// </summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>
        /// Current lobby config data
        /// </summary>
        public LobbyConfig Config { get; private set; }

        /// <summary>
        /// The allowed max number of players
        /// </summary>
        public int MaxPlayers { get; protected set; }

        /// <summary>
        /// The allowed min number of players
        /// </summary>
        public int MinPlayers { get; protected set; }

        /// <summary>
        /// Type of the lobby
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// IP address of the game
        /// </summary>
        public string GameIp { get; protected set; }

        /// <summary>
        /// Port of the game
        /// </summary>
        public int GamePort { get; protected set; }

        public BaseLobby(int lobbyId, IEnumerable<LobbyTeam> teams, LobbiesModule module, LobbyConfig config)
        {
            Logger = Mst.Create.Logger(typeof(BaseLobby).Name);

            Name = "lobby_" + Mst.Helper.CreateRandomDigitsString(5);

            Id = lobbyId;
            Module = module;
            GameIp = "";
            GamePort = -1;

            Config = config;

            controls = new List<LobbyPropertyData>();
            membersByUsernameList = new Dictionary<string, LobbyMember>();
            membersByPeerIdList = new Dictionary<int, LobbyMember>();
            propertiesList = new MstProperties();
            teamsList = teams.ToDictionary(t => t.Name, t => t);
            subscribersList = new HashSet<IPeer>();

            MaxPlayers = teamsList.Values.Sum(t => t.MaxPlayers);
            MinPlayers = teamsList.Values.Sum(t => t.MinPlayers);
        }

        /// <summary>
        /// Get or set the state of the lobby
        /// </summary>
        public LobbyState State
        {
            get
            {
                return _state;
            }
            protected set
            {
                if (_state == value)
                {
                    return;
                }

                _state = value;
                OnLobbyStateChange(value);
            }
        }

        /// <summary>
        /// Get or set the status info
        /// </summary>
        public string StatusText
        {
            get { return _statusText; }
            protected set
            {
                if (_statusText == value)
                {
                    return;
                }

                OnStatusTextChange(value);
            }
        }

        /// <summary>
        /// Get or set the game master
        /// </summary>
        protected LobbyMember GameMaster
        {
            get { return _gameMaster; }
            set
            {
                if (!Config.EnableGameMasters)
                {
                    return;
                }

                _gameMaster = value;
                OnGameMasterChange();
            }
        }

        /// <summary>
        /// Add player to lobby
        /// </summary>
        /// <param name="lobbyUser"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool AddPlayer(LobbyUserPeerExtension lobbyUser, out string error)
        {
            error = null;

            if (lobbyUser.CurrentLobby != null)
            {
                error = "You're already in a lobby";
                return false;
            }

            var username = TryGetUsername(lobbyUser.Peer);

            if (string.IsNullOrEmpty(username))
            {
                error = "Invalid username";
                return false;
            }

            if (membersByUsernameList.ContainsKey(username))
            {
                error = "Already in the lobby";
                return false;
            }

            if (IsDestroyed)
            {
                error = "Lobby is destroyed";
                return false;
            }

            if (!IsPlayerAllowed(username, lobbyUser))
            {
                error = "You're not allowed";
                return false;
            }

            if (membersByUsernameList.Values.Count >= MaxPlayers)
            {
                error = "Lobby is full";
                return false;
            }

            if (!Config.AllowJoiningWhenGameIsLive && State != LobbyState.Preparations)
            {
                error = "Game is already in progress";
                return false;
            }

            // Create an "instance" of the member
            var member = CreateMember(username, lobbyUser);

            // Add it to a team
            var team = PickTeamForPlayer(member);

            if (team == null)
            {
                error = "Invalid lobby team";
                return false;
            }

            if (!team.AddMember(member))
            {
                error = "Not allowed to join a team";
                return false;
            }

            membersByUsernameList[member.Username] = member;
            membersByPeerIdList[lobbyUser.Peer.Id] = member;

            // Set this lobby as player's current lobby
            lobbyUser.CurrentLobby = this;

            if (GameMaster == null)
            {
                PickNewGameMaster(false);
            }

            Subscribe(lobbyUser.Peer);

            lobbyUser.Peer.OnConnectionCloseEvent += OnPeerDisconnected;

            OnPlayerAdded(member);

            OnPlayerAddedEvent?.Invoke(member);

            return true;
        }

        /// <summary>
        /// Remove player from lobby
        /// </summary>
        /// <param name="lobbyUser"></param>
        public void RemovePlayer(LobbyUserPeerExtension lobbyUser)
        {
            var username = TryGetUsername(lobbyUser.Peer);

            membersByUsernameList.TryGetValue(username, out LobbyMember member);

            // If this player was never in the lobby
            if (member == null)
            {
                return;
            }

            membersByUsernameList.Remove(username);
            membersByPeerIdList.Remove(lobbyUser.Peer.Id);

            if (lobbyUser.CurrentLobby == this)
            {
                lobbyUser.CurrentLobby = null;
            }

            // Remove member from it's current team
            if (member.Team != null)
            {
                member.Team.RemoveMember(member);
            }

            // Change the game master
            if (GameMaster == member)
            {
                PickNewGameMaster();
            }


            // Unsubscribe
            lobbyUser.Peer.OnConnectionCloseEvent -= OnPeerDisconnected;
            Unsubscribe(lobbyUser.Peer);

            // Notify player himself that he's removed
            if (lobbyUser.Peer.IsConnected)
                lobbyUser.Peer.SendMessage((ushort)MstOpCodes.LeftLobby, Id);

            OnPlayerRemoved(member);
            OnPlayerRemovedEvent?.Invoke(member);
        }

        /// <summary>
        /// Get member of the lobby by extension
        /// </summary>
        /// <param name="playerExt"></param>
        /// <returns></returns>
        public LobbyMember GetMemberByExtension(LobbyUserPeerExtension playerExt)
        {
            membersByPeerIdList.TryGetValue(playerExt.Peer.Id, out LobbyMember member);
            return member;
        }

        /// <summary>
        /// Get member of the lobby by username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public LobbyMember GetMemberByUsername(string username)
        {
            membersByUsernameList.TryGetValue(username, out LobbyMember member);

            return member;
        }

        /// <summary>
        /// Get member of the lobby by peer id
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        public LobbyMember GetMemberByPeerId(int peerId)
        {
            LobbyMember member;
            membersByPeerIdList.TryGetValue(peerId, out member);

            return member;
        }

        /// <summary>
        /// Set a lobby player property
        /// </summary>
        /// <param name="member"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetPlayerProperty(LobbyMember member, string key, string value)
        {
            // Invalid property
            if (key == null)
            {
                return false;
            }

            // Check if player is allowed to change this property
            if (!IsPlayerPropertyChangeable(member, key, value))
            {
                return false;
            }

            member.SetProperty(key, value);

            OnPlayerPropertyChange(member, key);

            return true;
        }

        /// <summary>
        /// Set a lobby properties
        /// </summary>
        /// <param name="properties"></param>
        public void SetLobbyProperties(Dictionary<string, string> properties)
        {
            propertiesList.Append(properties);
        }

        /// <summary>
        /// Set the lobby property
        /// </summary>
        /// <param name="setter"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual bool SetProperty(LobbyUserPeerExtension setter, string key, string value)
        {
            if (!Config.AllowPlayersChangeLobbyProperties)
            {
                return false;
            }

            if (Config.EnableGameMasters)
            {
                membersByPeerIdList.TryGetValue(setter.Peer.Id, out LobbyMember member);

                if (GameMaster != member)
                {
                    return false;
                }
            }

            return SetProperty(key, value);
        }

        /// <summary>
        /// Set the lobby property
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetProperty(string key, string value)
        {
            propertiesList.Set(key, value);

            OnLobbyPropertyChange(key);
            return true;
        }

        /// <summary>
        /// Set the lobby member state as ready
        /// </summary>
        /// <param name="member"></param>
        /// <param name="state"></param>
        public void SetReadyState(LobbyMember member, bool state)
        {
            if (!membersByUsernameList.ContainsKey(member.Username))
            {
                return;
            }

            member.IsReady = state;

            OnPlayerReadyStatusChange(member);

            if (membersByUsernameList.Values.All(m => m.IsReady))
            {
                OnAllPlayersReady();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyData"></param>
        /// <param name="defaultValue"></param>
        public void AddControl(LobbyPropertyData propertyData, string defaultValue)
        {
            SetProperty(propertyData.PropertyKey, defaultValue);
            controls.Add(propertyData);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyData"></param>
        public void AddControl(LobbyPropertyData propertyData)
        {
            var defaultValue = "";

            if (propertyData.Options != null && propertyData.Options.Count > 0)
            {
                defaultValue = propertyData.Options.First();
            }

            SetProperty(propertyData.PropertyKey, defaultValue);
            controls.Add(propertyData);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="teamName"></param>
        /// <param name="member"></param>
        /// <returns></returns>
        public bool TryJoinTeam(string teamName, LobbyMember member)
        {
            if (!Config.EnableTeamSwitching)
            {
                return false;
            }

            var currentTeam = member.Team;
            var newTeam = teamsList[teamName];

            // Ignore, if any of the teams is invalid
            if (currentTeam == null || newTeam == null)
            {
                return false;
            }

            if (newTeam.PlayersCount >= newTeam.MaxPlayers)
            {
                SendChatMessage(member, "Team is full", true);
                return false;
            }

            // Try to add the member
            if (!newTeam.AddMember(member))
            {
                return false;
            }

            // Remove member from previous team
            currentTeam.RemoveMember(member);

            OnPlayerTeamChanged(member, newTeam);

            return true;
        }

        protected virtual LobbyMember CreateMember(string username, LobbyUserPeerExtension extension)
        {
            return new LobbyMember(username, extension);
        }

        protected virtual void PickNewGameMaster(bool broadcastChange = true)
        {
            if (!Config.EnableGameMasters)
            {
                return;
            }

            GameMaster = membersByUsernameList.Values.FirstOrDefault();
        }

        public virtual LobbyTeam PickTeamForPlayer(LobbyMember member)
        {
            return teamsList.Values
                .Where(t => t.CanAddPlayer(member))
                .OrderBy(t => t.PlayersCount).FirstOrDefault();
        }

        /// <summary>
        /// Extracts username of the peer.
        /// By default, uses user extension <see cref="IUserPeerExtension"/>
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        protected virtual string TryGetUsername(IPeer peer)
        {
            var userExt = peer.GetExtension<IUserPeerExtension>();

            if (userExt == null)
            {
                return null;
            }

            return userExt.Username;
        }

        /// <summary>
        /// This will be called before adding a player to lobby.
        /// Override it to add custom checks for bans and etc.
        /// </summary>
        protected virtual bool IsPlayerAllowed(string username, LobbyUserPeerExtension user)
        {
            return true;
        }

        protected virtual bool IsPlayerPropertyChangeable(LobbyMember member, string key, string value)
        {
            return true;
        }

        public void Subscribe(IPeer peer)
        {
            subscribersList.Add(peer);
        }

        public void Unsubscribe(IPeer peer)
        {
            subscribersList.Remove(peer);
        }

        public virtual bool StartGame()
        {
            if (IsDestroyed)
            {
                return false;
            }

            // Extract the region if available
            string region = propertiesList.AsString(Mst.Args.Names.RoomRegion);

            // Set room as private because this one is created for lobby
            propertiesList.Set(Mst.Args.Names.RoomIsPrivate, false);
            propertiesList.Append(GenerateOptions());

            // Create start task
            var task = Module.SpawnersModule.Spawn(propertiesList, region);

            if (task == null)
            {
                BroadcastChatMessage("Servers are busy", true);
                return false;
            }

            // Set starting status
            State = LobbyState.StartingGameServer;

            // Set task
            SetGameSpawnTask(task);

            return true;
        }

        public void SetGameSpawnTask(SpawnTask task)
        {
            if (task == null || gameSpawnTask == task)
            {
                return;
            }

            if (gameSpawnTask != null)
            {
                // Unsubscribe from previous game
                gameSpawnTask.OnStatusChangedEvent -= OnSpawnServerStatusChanged;
                gameSpawnTask.Abort();
            }

            gameSpawnTask = task;
            gameSpawnTask.OnStatusChangedEvent += OnSpawnServerStatusChanged;
        }

        public void Destroy()
        {
            if (IsDestroyed)
            {
                return;
            }

            IsDestroyed = true;

            // Remove players
            foreach (var member in membersByUsernameList.Values.ToList())
            {
                RemovePlayer(member.Extension);
            }

            if (gameSpawnTask != null)
            {
                gameSpawnTask.OnStatusChangedEvent -= OnSpawnServerStatusChanged;
                gameSpawnTask.KillSpawnedProcess();
            }

            OnDestroyedEvent?.Invoke(this);
        }

        protected virtual MstProperties GenerateOptions()
        {
            var options = new MstProperties();
            options.Set(Mst.Args.Names.LobbyId, Id.ToString());
            return options;
        }

        protected virtual void OnSpawnServerStatusChanged(SpawnStatus status)
        {
            var isStarting = status > SpawnStatus.None && status < SpawnStatus.Finalized;

            // If the game is currently starting
            if (isStarting && State != LobbyState.StartingGameServer)
            {
                State = LobbyState.StartingGameServer;
                return;
            }

            // If game is running
            if (status == SpawnStatus.Finalized)
            {
                State = LobbyState.GameInProgress;
                OnGameServerFinalized();
            }

            // If game is aborted / closed
            if (status < SpawnStatus.None)
            {
                // If game was open before
                if (State == LobbyState.StartingGameServer)
                {
                    State = Config.PlayAgainEnabled ? LobbyState.Preparations : LobbyState.FailedToStart;
                    BroadcastChatMessage("Failed to start a game server", true);
                }
                else
                {
                    State = Config.PlayAgainEnabled ? LobbyState.Preparations : LobbyState.GameOver;
                }
            }
        }

        protected virtual void OnGameServerFinalized()
        {
            if (gameSpawnTask.FinalizationPacket == null)
            {
                return;
            }

            var data = gameSpawnTask.FinalizationPacket.FinalizationData;

            if (!data.Has(MstDictKeys.ROOM_ID))
            {
                BroadcastChatMessage("Game server finalized, but room ID cannot be found", true);
                return;
            }

            // Get room id from finalization data
            var roomId = data.AsInt(MstDictKeys.ROOM_ID);
            var room = Module.RoomsModule.GetRoom(roomId);

            if (room == null)
            {
                return;
            }

            lobbyRoom = room;

            GameIp = room.Options.RoomIp;
            GamePort = room.Options.RoomPort;

            room.OnDestroyedEvent += OnRoomDestroyed;
        }

        public void OnRoomDestroyed(RegisteredRoom room)
        {
            room.OnDestroyedEvent -= OnRoomDestroyed;

            GameIp = "";
            GamePort = -1;
            lobbyRoom = null;

            gameSpawnTask = null;

            State = Config.PlayAgainEnabled ? LobbyState.Preparations : LobbyState.GameOver;
        }

        public MstProperties GetPublicProperties(IPeer peer)
        {
            return propertiesList;
        }

        #region Packet generators

        public LobbyDataPacket GenerateLobbyData()
        {
            var info = new LobbyDataPacket
            {
                LobbyType = Type ?? "",
                GameMaster = GameMaster != null ? GameMaster.Username : "",
                LobbyName = Name,
                LobbyId = Id,
                LobbyProperties = propertiesList.ToDictionary(),
                Members = membersByUsernameList.Values.ToDictionary(m => m.Username, GenerateMemberData),
                Teams = teamsList.Values.ToDictionary(t => t.Name, t => t.GenerateData()),
                Controls = controls,
                LobbyState = State,
                MaxPlayers = MaxPlayers,
                EnableTeamSwitching = Config.EnableTeamSwitching,
                EnableReadySystem = Config.EnableReadySystem,
                EnableManualStart = Config.EnableManualStart,
                CurrentUserUsername = ""
            };

            return info;
        }

        public LobbyDataPacket GenerateLobbyData(LobbyUserPeerExtension user)
        {
            var info = new LobbyDataPacket
            {
                LobbyType = Type ?? "",
                GameMaster = GameMaster != null ? GameMaster.Username : "",
                LobbyName = Name,
                LobbyId = Id,
                LobbyProperties = propertiesList.ToDictionary(),
                Members = membersByUsernameList.Values.ToDictionary(m => m.Username, GenerateMemberData),
                Teams = teamsList.Values.ToDictionary(t => t.Name, t => t.GenerateData()),
                Controls = controls,
                LobbyState = State,
                MaxPlayers = MaxPlayers,
                EnableTeamSwitching = Config.EnableTeamSwitching,
                EnableReadySystem = Config.EnableReadySystem,
                EnableManualStart = Config.EnableManualStart,
                CurrentUserUsername = TryGetUsername(user.Peer)
            };

            return info;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="member"></param>
        /// <param name="message"></param>
        public void ChatMessageHandler(LobbyMember member, IIncomingMessage message)
        {
            var text = message.AsString();

            var messagePacket = new LobbyChatPacket()
            {
                Message = text,
                Sender = member.Username
            };

            var msg = MessageHelper.Create((ushort)MstOpCodes.LobbyChatMessage, messagePacket.ToBytes());

            Broadcast(msg);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void GameAccessRequestHandler(IIncomingMessage message)
        {
            if (lobbyRoom == null)
            {
                message.Respond("Game is not running", ResponseStatus.Failed);
                return;
            }

            var requestData = MstProperties.FromBytes(message.AsBytes());

            lobbyRoom.GetAccess(message.Peer, requestData, (access, error) =>
            {
                if (access == null)
                {
                    message.Respond(error ?? "Failed to get access to game", ResponseStatus.Failed);
                    return;
                }

                // Send back the access
                message.Respond(access, ResponseStatus.Success);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public virtual bool StartGameManually(LobbyUserPeerExtension user)
        {
            var member = GetMemberByExtension(user);

            if (!Config.EnableManualStart)
            {
                SendChatMessage(member, "You cannot start the game manually", true);
                return false;
            }

            // If not game master
            if (GameMaster != member)
            {
                SendChatMessage(member, "You're not the master of this game", true);
                return false;
            }

            if (State != LobbyState.Preparations)
            {
                SendChatMessage(member, "Invalid lobby state", true);
                return false;
            }

            if (IsDestroyed)
            {
                SendChatMessage(member, "Lobby is destroyed", true);
                return false;
            }

            if (membersByUsernameList.Values.Any(m => !m.IsReady && m != _gameMaster))
            {
                SendChatMessage(member, "Not all players are ready", true);
                return false;
            }

            if (membersByUsernameList.Count < MinPlayers)
            {
                SendChatMessage(
                    member,
                    string.Format("Not enough players. Need {0} more ", (MinPlayers - membersByUsernameList.Count)),
                    true);
                return false;
            }

            var lackingTeam = teamsList.Values.FirstOrDefault(t => t.MinPlayers > t.PlayersCount);

            if (lackingTeam != null)
            {
                var msg = string.Format("Team {0} does not have enough players", lackingTeam.Name);
                SendChatMessage(member, msg, true);
                return false;
            }

            return StartGame();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        public virtual LobbyMemberData GenerateMemberData(LobbyMember member)
        {
            return member.GenerateDataPacket();
        }

        #endregion

        #region Broadcasting

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void Broadcast(IOutgoingMessage message)
        {
            foreach (var peer in subscribersList)
            {
                peer.SendMessage(message, DeliveryMethod.Reliable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="condition"></param>
        public void Broadcast(IOutgoingMessage message, Func<IPeer, bool> condition)
        {
            foreach (var peer in subscribersList)
            {
                if (!condition(peer))
                {
                    continue;
                }

                peer.SendMessage(message, DeliveryMethod.Reliable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="isError"></param>
        /// <param name="sender"></param>
        public void BroadcastChatMessage(string message, bool isError = false, string sender = "System")
        {
            var msg = new LobbyChatPacket()
            {
                Message = message,
                Sender = sender,
                IsError = isError
            };

            Broadcast(MessageHelper.Create((ushort)MstOpCodes.LobbyChatMessage, msg.ToBytes()));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="member"></param>
        /// <param name="message"></param>
        /// <param name="isError"></param>
        /// <param name="sender"></param>
        public void SendChatMessage(LobbyMember member, string message, bool isError = false, string sender = "System")
        {
            var packet = new LobbyChatPacket()
            {
                Message = message,
                Sender = sender,
                IsError = isError
            };

            var msg = MessageHelper.Create((ushort)MstOpCodes.LobbyChatMessage, packet.ToBytes());

            member.Extension.Peer.SendMessage(msg, DeliveryMethod.Reliable);
        }

        #endregion

        #region On... Stuff

        /// <summary>
        /// 
        /// </summary>
        /// <param name="member"></param>
        protected virtual void OnPlayerAdded(LobbyMember member)
        {
            // Notify others about the new user
            var msg = MessageHelper.Create((ushort)MstOpCodes.LobbyMemberJoined, member.GenerateDataPacket().ToBytes());

            // Don't send to the person who just joined
            Broadcast(msg, p => p != member.Extension.Peer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="member"></param>
        protected virtual void OnPlayerRemoved(LobbyMember member)
        {
            // Destroy lobby if last member left
            if (!Config.KeepAliveWithZeroPlayers && membersByUsernameList.Count == 0)
            {
                Destroy();
                Logger.Log(LogLevel.Info, string.Format("Lobby \"{0}\" destroyed due to last player leaving.", Name));
            }

            // Notify others about the user who left
            Broadcast(MessageHelper.Create((ushort)MstOpCodes.LobbyMemberLeft, member.Username));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        protected virtual void OnLobbyStateChange(LobbyState state)
        {
            switch (state)
            {
                case LobbyState.FailedToStart:
                    StatusText = "Failed to start server";
                    break;
                case LobbyState.Preparations:
                    StatusText = "Failed to start server";
                    break;
                case LobbyState.StartingGameServer:
                    StatusText = "Starting game server";
                    break;
                case LobbyState.GameInProgress:
                    StatusText = "Game in progress";
                    break;
                case LobbyState.GameOver:
                    StatusText = "Game is over";
                    break;
                default:
                    StatusText = "Unknown lobby state";
                    break;
            };

            // Disable ready states
            foreach (var lobbyMember in membersByUsernameList.Values)
            {
                SetReadyState(lobbyMember, false);
            }

            var msg = MessageHelper.Create((ushort)MstOpCodes.LobbyStateChange, (int)state);
            Broadcast(msg);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        private void OnStatusTextChange(string text)
        {
            var msg = MessageHelper.Create((ushort)MstOpCodes.LobbyStatusTextChange, text);
            Broadcast(msg);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyKey"></param>
        protected virtual void OnLobbyPropertyChange(string propertyKey)
        {
            var packet = new StringPairPacket()
            {
                A = propertyKey,
                B = propertiesList.AsString(propertyKey)
            };

            // Broadcast new properties
            Broadcast(MessageHelper.Create((ushort)MstOpCodes.LobbyPropertyChanged, packet.ToBytes()));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="member"></param>
        /// <param name="propertyKey"></param>
        protected virtual void OnPlayerPropertyChange(LobbyMember member, string propertyKey)
        {
            // Broadcast the changes
            var changesPacket = new LobbyMemberPropChangePacket()
            {
                LobbyId = Id,
                Username = member.Username,
                Property = propertyKey,
                Value = member.GetProperty(propertyKey)
            };

            Broadcast(MessageHelper.Create((ushort)MstOpCodes.LobbyMemberPropertyChanged, changesPacket.ToBytes()));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="member"></param>
        /// <param name="newTeam"></param>
        protected virtual void OnPlayerTeamChanged(LobbyMember member, LobbyTeam newTeam)
        {
            var packet = new StringPairPacket()
            {
                A = member.Username,
                B = newTeam.Name
            };

            // Broadcast the change
            var msg = MessageHelper.Create((ushort)MstOpCodes.LobbyMemberChangedTeam, packet.ToBytes());
            Broadcast(msg);
        }

        /// <summary>
        /// Invoked when one of the members disconnects
        /// </summary>
        /// <param name="session"></param>
        protected virtual void OnPeerDisconnected(IPeer peer)
        {
            RemovePlayer(peer.GetExtension<LobbyUserPeerExtension>());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="member"></param>
        protected virtual void OnPlayerReadyStatusChange(LobbyMember member)
        {
            // Broadcast the new status
            var packet = new StringPairPacket()
            {
                A = member.Username,
                B = member.IsReady.ToString()
            };

            Broadcast(MessageHelper.Create((ushort)MstOpCodes.LobbyMemberReadyStatusChange, packet.ToBytes()));
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnGameMasterChange()
        {
            var masterUsername = GameMaster != null ? GameMaster.Username : "";
            var msg = MessageHelper.Create((ushort)MstOpCodes.LobbyMasterChange, masterUsername);
            Broadcast(msg);
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnAllPlayersReady()
        {
            if (!Config.StartGameWhenAllReady)
            {
                return;
            }

            if (teamsList.Values.Any(t => t.PlayersCount < t.MinPlayers))
            {
                return;
            }

            StartGame();
        }

        #endregion
    }
}