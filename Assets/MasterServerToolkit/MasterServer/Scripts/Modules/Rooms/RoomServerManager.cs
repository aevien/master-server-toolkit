using MasterServerToolkit.Extensions;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public class RoomEvent : UnityEvent<RoomOptions> { }
    [Serializable]
    public class RoomRegisterEvent : UnityEvent<RoomController> { }
    [Serializable]
    public class RoomPlayerEvent : UnityEvent<RoomPlayer> { }

    public delegate ObservableServerProfile ProfileFactoryDelegate(string userId);

    public class RoomServerManager : BaseClientBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Loads player profile after he joined the room
        /// </summary>
        [Header("Room Settings"), SerializeField, Tooltip("Requests and attaches the player's authoritative profile after room access is validated. Disable only when the room does not use MST profiles or loads them through custom code.")]
        protected bool autoLoadUserProfile = true;

        /// <summary>
        /// Fires when server room is successfully registered
        /// </summary>
        [Header("Room Events")]
        [Tooltip("Invoked after the master accepts this room registration. The argument is the active Room Controller used to update or destroy the registration.")]
        public RoomRegisterEvent OnRoomRegisteredEvent;

        /// <summary>
        /// Fires when server room registration failed
        /// </summary>
        [Tooltip("Invoked when this room cannot register with the master. Inspect the MST log for the rejection or connection reason.")]
        public UnityEvent OnRoomRegisterFailedEvent;

        /// <summary>
        /// Fires when new playerjoined room
        /// </summary>
        [Tooltip("Invoked after a player's room access is validated and the Room Player is added. When profile auto-load is enabled, the profile has been attached before this event.")]
        public RoomPlayerEvent OnPlayerJoinedRoomEvent;

        /// <summary>
        /// Fires when existing player left room
        /// </summary>
        [Tooltip("Invoked after a Room Player is removed because of leave or disconnect and its profile is disposed. Do not retain the argument or use its Profile after this callback.")]
        public RoomPlayerEvent OnPlayerLeftRoomEvent;

        #endregion

        private readonly Dictionary<int, RoomPlayer> players = new();
        private RoomOptions roomOptions;

        /// <summary>
        /// Controller of the room
        /// </summary>
        public RoomController Controller { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsActive => Controller != null && Controller.IsActive;

        /// <summary>
        /// Spawner task controller
        /// </summary>
        public SpawnTaskController SpawnTaskController { get; protected set; }

        /// <summary>
        /// List of all players
        /// </summary>
        public IEnumerable<RoomPlayer> Players => players.Values;

        /// <summary>
        /// Check if room has players
        /// </summary>
        public bool HasPlayers => players.Any();

        /// <summary>
        /// By default, profiles module will use this factory to create a profile for users.
        /// If you're using profiles, you will need to change this factory to construct the
        /// structure of a profile.
        /// </summary>
        public ProfileFactoryDelegate ProfileFactory { get; set; }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Server logic
            if (!Mst.Client.Rooms.IsClient)
            {
                StopAllCoroutines();
                CancelInvoke();

                Connection?.RemoveConnectionOpenListener(OnConnectedToMasterEventHandler);
                Connection?.Close();
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            // Server logic
            if (!Mst.Client.Rooms.IsClient)
            {
                ProfileFactory = (userId) => new ObservableServerProfile(userId);
            }
        }

        /// <summary>
        /// Invoke this method when server is started
        /// </summary>
        public virtual void StartServer()
        {
            // Listen to master server connection status
            Connection.AddConnectionOpenListener(OnConnectedToMasterEventHandler);
        }

        /// <summary>
        /// Invoke this method when server is stopped
        /// </summary>
        public virtual void StopServer()
        {
            Controller?.Destroy();
        }

        /// <summary>
        /// Gets all room options
        /// </summary>
        /// <returns></returns>
        public RoomOptions Options()
        {
            if (roomOptions == null)
            {
                bool isUsingLobby = Mst.Args.IsProvided(Mst.Args.Names.LobbyId);
                bool isPublic = !Mst.Args.RoomIsPrivate || isUsingLobby;

                roomOptions = new RoomOptions
                {
                    Name = Mst.Args.AsString(Mst.Args.Names.RoomTitle, $"Room-{Mst.Helper.CreateRandomDigitsString(6)}").Unescape(),
                    RoomIp = Mst.Args.RoomIp,
                    RoomPort = Mst.Args.RoomPort,
                    IsPublic = isPublic,
                    MaxPlayers = Mst.Args.RoomMaxConnections,
                    Password = Mst.Args.RoomPassword,
                    Region = Mst.Args.RoomRegion,
                };

                string[] keys = Mst.Args.FindKeys(MstParamKeys.ROOM_EXTRA_PARAMS_PREFIX);
                var properties = new MstProperties();

                foreach (string key in keys)
                {
                    if (Mst.Args.IsProvided(key))
                        properties.Set(key, Mst.Args.AsString(key));
                }

                roomOptions.ExtraParameters.Append(properties.UnescapeValues());
            }

            return roomOptions;
        }

        private void OnConnectedToMasterEventHandler(IClientSocket client)
        {
            Logger.Info("The room manager has successfully connected to the master server");

            if (Mst.Server.Spawners.IsSpawnedProccess)
            {
                RegisterSpawnedProcess();
            }
            else
            {
                RegisterRoom();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomPeerId"></param>
        public virtual void OnPeerDisconnected(int roomPeerId)
        {
            // Try to find player in filtered list
            if (players.TryGetValue(roomPeerId, out RoomPlayer player))
            {
                Logger.Info($"The player [{player.Username}] has just left the room server");

                // Remove this player from list
                players.Remove(player.RoomPeerId);

                // Notify master server about disconnected player
                Controller?.NotifyPlayerLeft(player.MasterPeerId);

                // Dispose profile
                player.Profile?.Dispose();

                OnPlayerLeftRoom(player);

                // Inform subscribers about this bad guy
                OnPlayerLeftRoomEvent?.Invoke(player);
            }
            else
            {
                Logger.Debug($"The client {roomPeerId} has just left the room server");
            }
        }

        /// <summary>
        /// Invoke this method to validate access token
        /// </summary>
        /// <param name="token"></param>
        public virtual void ValidateRoomAccess(int roomPeerId, string token, SuccessCallback callback)
        {
            ValidateRoomAccess(roomPeerId, token, callback, CancellationToken.None);
        }

        /// <summary>
        /// Validates an access token and stops the join workflow when the server run is cancelled.
        /// </summary>
        public virtual void ValidateRoomAccess(int roomPeerId, string token, SuccessCallback callback,
            CancellationToken cancellationToken)
        {
            ValidateRoomAccess(roomPeerId, token, callback, cancellationToken, null);
        }

        /// <summary>
        /// Validates room access and atomically claims the final join mutation when required.
        /// </summary>
        public virtual void ValidateRoomAccess(int roomPeerId, string token, SuccessCallback callback,
            CancellationToken cancellationToken, Func<bool> tryBeginFinalization)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            // Triying to validate given token
            Mst.Server.Rooms.ValidateAccess(Controller.RoomId, token, (usernameAndPeerId, error) =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    // If token is not valid
                    if (usernameAndPeerId == null)
                    {
                        Logger.Error($"Room access validation failed. roomPeerId={roomPeerId}");
                        callback?.Invoke(false, error);
                        return;
                    }

                    Logger.Info($"The room server client [{roomPeerId}] is successfully validated. Trying to load his account info...");

                    Mst.Server.Auth.GetAccountInfoByPeer(usernameAndPeerId.PeerId, (accountInfo, accountError) =>
                    {
                        RoomPlayer player = null;

                        try
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            if (accountInfo == null)
                            {
                                Logger.Error($"The account of the room server client [{roomPeerId}] could not be loaded");
                                callback?.Invoke(false, accountError);
                                return;
                            }

                            var accountProperties = new MstProperties(accountInfo.ExtraProperties);

                            Logger.Info($"The account of the player [{accountInfo.Username}:{roomPeerId}] has just been loaded");

                            // Keep the player provisional until all required data is loaded.
                            player = new RoomPlayer(usernameAndPeerId.PeerId, roomPeerId, accountInfo.UserId, accountInfo.Username, accountProperties)
                            {
                                Profile = ProfileFactory(accountInfo.UserId)
                            };

                            // If server is required user profile
                            if (autoLoadUserProfile)
                            {
                                LoadPlayerProfile(player, (isLoadProfileSuccess, loadProfileError) =>
                                {
                                    try
                                    {
                                        if (cancellationToken.IsCancellationRequested)
                                        {
                                            player.Profile?.Dispose();
                                            return;
                                        }

                                        if (!isLoadProfileSuccess)
                                        {
                                            player.Profile?.Dispose();
                                            callback?.Invoke(false, loadProfileError);
                                            return;
                                        }

                                        CompletePlayerJoining(
                                            player,
                                            callback,
                                            cancellationToken,
                                            tryBeginFinalization);
                                    }
                                    catch (Exception e)
                                    {
                                        player.Profile?.Dispose();

                                        if (!cancellationToken.IsCancellationRequested)
                                        {
                                            Logger.Error($"Room profile loading callback failed. roomPeerId={roomPeerId}, error={e}");
                                            callback?.Invoke(false,
                                                Mst.Errors.Parse(ResponseStatus.Error));
                                        }
                                    }
                                });
                            }
                            else
                            {
                                CompletePlayerJoining(
                                    player,
                                    callback,
                                    cancellationToken,
                                    tryBeginFinalization);
                            }
                        }
                        catch (Exception e)
                        {
                            player?.Profile?.Dispose();

                            if (!cancellationToken.IsCancellationRequested)
                            {
                                Logger.Error($"Room account loading callback failed. roomPeerId={roomPeerId}, error={e}");
                                callback?.Invoke(false,
                                    Mst.Errors.Parse(ResponseStatus.Error));
                            }
                        }
                    }, Connection);
                }
                catch (Exception e)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Logger.Error($"Room access validation callback failed. roomPeerId={roomPeerId}, error={e}");
                        callback?.Invoke(false,
                            Mst.Errors.Parse(ResponseStatus.Error));
                    }
                }
            }, Connection);
        }

        private void CompletePlayerJoining(RoomPlayer player, SuccessCallback callback,
            CancellationToken cancellationToken, Func<bool> tryBeginFinalization)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                player.Profile?.Dispose();
                return;
            }

            if (tryBeginFinalization != null && !tryBeginFinalization.Invoke())
            {
                player.Profile?.Dispose();
                return;
            }

            try
            {
                players.Add(player.RoomPeerId, player);
                FinalizePlayerJoining(player.RoomPeerId);
            }
            catch (Exception e)
            {
                players.Remove(player.RoomPeerId);
                player.Profile?.Dispose();
                Logger.Error($"Room player finalization failed. roomPeerId={player.RoomPeerId}, error={e}");
                callback?.Invoke(false,
                    Mst.Errors.Parse(ResponseStatus.Error));

                return;
            }

            callback?.Invoke(true, string.Empty);
        }

        /// <summary>
        /// Before we register our room we need to register spawned process if required
        /// </summary>
        protected void RegisterSpawnedProcess()
        {
            Logger.Info("Registering spawned process...");

            Mst.Server.Spawners.RegisterSpawnedProcess(Mst.Args.SpawnTaskId, Mst.Args.SpawnTaskUniqueCode, (taskController, error) =>
            {
                if (taskController == null)
                {
                    Logger.Error("Room server process cannot be registered");
                    return;
                }

                Logger.Info($"Room server process registered with task ID: {Mst.Args.SpawnTaskId}");

                SpawnTaskController = taskController;
            }, Connection);
        }

        /// <summary>
        /// Start registering our room server
        /// </summary>
        protected virtual void RegisterRoom()
        {
            Logger.Info($"Registering room {roomOptions.Name} to list...");

            Mst.Server.Rooms.RegisterRoom(roomOptions, (controller, error) =>
            {
                if (controller == null)
                {
                    Logger.Error($"Room registration failed. roomName={roomOptions.Name}");
                    OnRoomRegisterFailedEvent?.Invoke();
                    return;
                }

                // Registered room controller
                Controller = controller;

                // Set access provider
                Controller.AccessProvider = CreateAccessProvider;

                // And save them
                Controller.SaveOptions();

                Logger.Info($"The room server registered successfully. ID: {controller.RoomId}, Options: {roomOptions}");

                // If this room was spawned
                SpawnTaskController?.FinalizeTask(CreateSpawnFinalizationData());

                // Notify listeners
                OnRoomRegisteredEvent?.Invoke(Controller);
            }, Connection);
        }

        /// <summary>
        /// This <see cref="MstProperties"/> will be sent to "master server" when we want 
        /// notify "master" server that Spawn Process is completed
        /// </summary>
        /// <returns></returns>
        protected virtual MstProperties CreateSpawnFinalizationData()
        {
            var properties = new MstProperties();
            properties.Set(MstParamKeys.ROOM_ID, Controller.RoomId);
            properties.Set(Mst.Args.Names.RoomPassword, Controller.Options.Password);
            return properties;
        }

        /// <summary>
        /// Override, if you want to manually handle creation of access'es
        /// </summary>
        /// <param name="accessCheckOptions"></param>
        /// <param name="giveAccess"></param>
        protected virtual void CreateAccessProvider(RoomAccessProviderCheck accessCheckOptions, RoomAccessProviderCallbackDelegate giveAccess)
        {
            // Use accessCheckOptions to check user that requested access to room
            giveAccess.Invoke(new RoomAccessPacket()
            {
                Id = Controller.RoomId,
                Ip = Controller.Options.RoomIp,
                Port = Controller.Options.RoomPort,
                MaxPlayers = Controller.Options.MaxPlayers,
                ExtraParameters = Controller.Options.ExtraParameters,
                Token = Mst.Helper.CreateRandomAlphanumericString(16),
                SceneName = Mst.Args.AsString(Mst.Args.Names.RoomOnlineScene, SceneManager.GetActiveScene().name)
            }, null);
        }

        /// <summary>
        /// Finalize player joining to server room
        /// </summary>
        /// <param name="conn"></param>
        protected virtual void FinalizePlayerJoining(int roomPeerId)
        {
            if (players.ContainsKey(roomPeerId))
            {
                RoomPlayer player = players[roomPeerId];
                Logger.Info($"A new player has just connected to the room. Username is [{player.Username}]");

                OnPlayerJoinedRoom(player);
                OnPlayerJoinedRoomEvent?.Invoke(player);
            }
        }

        /// <summary>
        /// Invoked when player joins a room
        /// </summary>
        /// <param name="player"></param>
        protected virtual void OnPlayerJoinedRoom(RoomPlayer player)
        {
            MstTimer.WaitForSeconds(2f, () =>
            {
                Mst.Server.Notifications.NotifyRecipient(player.MasterPeerId,
                            $"Hi, {player.Username}!\nWelcome to \"{roomOptions.Name}\" server", null, Connection);
            });

            Mst.Server.Notifications.NotifyRoom(Controller.RoomId,
                    new int[] { player.MasterPeerId },
                    $"A new player has just connected to the room. Username is [{player.Username}]",
                    null, Connection);
        }

        /// <summary>
        /// Invoked when player leaves a room
        /// </summary>
        /// <param name="player"></param>
        protected virtual void OnPlayerLeftRoom(RoomPlayer player)
        {
            Mst.Server.Notifications.NotifyRoom(Controller.RoomId,
                    new int[] { player.MasterPeerId },
                    $"Player {player.Username} has just left the room",
                    null, Connection);
        }

        /// <summary>
        /// Loads player profile
        /// </summary>
        /// <param name="successCallback"></param>
        public void LoadPlayerProfile(string username, SuccessCallback successCallback)
        {
            if (TryGetRoomPlayerByUsername(username, out RoomPlayer player))
            {
                LoadPlayerProfile(player, successCallback);
            }
        }

        private void LoadPlayerProfile(RoomPlayer player, SuccessCallback successCallback)
        {
            Logger.Info($"Trying to load prfile of player {player.Username}:{player.RoomPeerId}");

            Mst.Server.Profiles.FillProfileValues(player.Profile, (isSuccess, error) =>
            {
                if (!isSuccess)
                {
                    string message = $"Room server cannot load profile of player {player.Username}:{player.RoomPeerId} from master server";
                    Logger.Error(message);
                    successCallback?.Invoke(false, message);
                    return;
                }

                Logger.Debug($"Profile of player {player.Username}:{player.RoomPeerId} is successfully loaded. Player info: {player}");
                successCallback?.Invoke(true, string.Empty);
            }, Connection);
        }

        /// <summary>
        /// Get <see cref="RoomPlayer"/> by room peer Id
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        public RoomPlayer GetRoomPlayerByRoomPeer(int peerId)
        {
            players.TryGetValue(peerId, out RoomPlayer player);
            return player;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="roomPlayer"></param>
        /// <returns></returns>
        public bool TryGetRoomPlayerByRoomPeer(int peerId, out RoomPlayer roomPlayer)
        {
            roomPlayer = GetRoomPlayerByRoomPeer(peerId);
            return roomPlayer != null;
        }

        /// <summary>
        /// Returns <see cref="RoomPlayer"/> by master peer Id
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        public RoomPlayer GetRoomPlayerByMasterPeer(int peerId)
        {
            return players.Values.Where(i => i.MasterPeerId == peerId).FirstOrDefault();
        }

        /// <summary>
        /// Returns <see cref="RoomPlayer"/> by master peer Id
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="roomPlayer"></param>
        /// <returns></returns>
        public bool TryGetRoomPlayerByMasterPeer(int peerId, out RoomPlayer roomPlayer)
        {
            roomPlayer = GetRoomPlayerByMasterPeer(peerId);
            return roomPlayer != null;
        }

        /// <summary>
        /// Get <see cref="RoomPlayer"/> by master username
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public RoomPlayer GetRoomPlayerByUsername(string username)
        {
            return players.Values.Where(i => i.Username == username).FirstOrDefault();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="roomPlayer"></param>
        /// <returns></returns>
        public bool TryGetRoomPlayerByUsername(string username, out RoomPlayer roomPlayer)
        {
            roomPlayer = GetRoomPlayerByUsername(username);
            return roomPlayer != null;
        }
    }
}
