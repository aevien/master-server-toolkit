using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class RoomsModule : BaseServerModule, IGamesProvider
    {
        private sealed class OwnerRoomsState
        {
            public OwnerRoomsState(IPeer peer)
            {
                Peer = peer;
            }

            public object Gate { get; } = new object();
            public IPeer Peer { get; }
            public ConcurrentDictionary<int, RegisteredRoom> Rooms { get; } =
                new ConcurrentDictionary<int, RegisteredRoom>();
            public bool IsActive { get; set; } = true;
        }

        #region Unity Inspector

        [SerializeField, Tooltip("Interval in realtime seconds between removal passes for disconnected rooms and expired unconfirmed access reservations. Use a positive value; smaller values clean faster but run the scans more often.")]
        private float cleanRate = 1.0f;
        [SerializeField, Min(1), Tooltip("Maximum seconds the master waits for an active room to save and release a player session during a trusted account takeover. Increase this when profile persistence or room leave confirmation can legitimately take longer.")]
        private int playerSessionReleaseTimeoutSeconds = 30;

        #endregion

        /// <summary>
        /// ID of the last created room
        /// </summary>
        private int lastRoomId = -1;

        /// <summary>
        /// Registered rooms list
        /// </summary>
        protected readonly ConcurrentDictionary<int, RegisteredRoom> roomsList = new ConcurrentDictionary<int, RegisteredRoom>();
        private readonly ConcurrentDictionary<int, OwnerRoomsState> ownerRoomsByPeerId =
            new ConcurrentDictionary<int, OwnerRoomsState>();
        private readonly object runGate = new object();
        private bool acceptsRoomRegistrations = true;

        /// <summary>
        /// Fired when new room is registered
        /// </summary>
        public event Action<RegisteredRoom> OnRoomRegisteredEvent;

        /// <summary>
        /// Fired when existing room is destroyed
        /// </summary>
        public event Action<RegisteredRoom> OnRoomDestroyedEvent;

        /// <summary>
        /// Allocates the next room id.
        /// </summary>
        /// <returns></returns>
        public int AllocateRoomId()
        {
            return Interlocked.Increment(ref lastRoomId);
        }

        public override void Initialize(IServer server)
        {
            // Add handlers
            server.RegisterMessageHandler(MstOpCodes.RegisterRoomRequest, RegisterRoomRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.DestroyRoomRequest, DestroyRoomRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.SaveRoomOptionsRequest, SaveRoomOptionsRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.GetRoomAccessRequest, GetRoomAccessRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ValidateRoomAccessRequest, ValidateRoomAccessRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.PlayerLeftRoomRequest, PlayerLeftRoomRequestHandler);

        }

        public override void StartServerRun(CancellationToken runCancellationToken)
        {
            lock (runGate)
                acceptsRoomRegistrations = true;

            InvokeRepeating(nameof(CleanUnconfirmedAccesses), cleanRate, cleanRate);
            InvokeRepeating(nameof(CleanInvalidRooms), cleanRate, cleanRate);
        }

        public override Task StopServerRunAsync()
        {
            List<RegisteredRoom> registeredRooms;

            lock (runGate)
            {
                acceptsRoomRegistrations = false;
                registeredRooms = roomsList.Values.ToList();
            }

            CancelInvoke(nameof(CleanUnconfirmedAccesses));
            CancelInvoke(nameof(CleanInvalidRooms));

            foreach (RegisteredRoom room in registeredRooms)
                DestroyRoom(room);

            ownerRoomsByPeerId.Clear();
            return Task.CompletedTask;
        }

        public override MstJson Details()
        {
            int totalPlayers = 0;

            var info = base.Details();
            info.SetField("description", "This module handles the creation, management, and distribution of game rooms, ensuring players connect to the correct sessions and controlling their lifecycle.");
            info["properties"].AddField("rooms", MstJson.CreateArray());

            foreach (var room in roomsList.Values)
            {
                if (!room.TryGetSnapshot(out RoomOptions options, out _, out int onlineCount))
                    continue;

                totalPlayers += onlineCount;

                var roomJson = MstJson.CreateObject();

                roomJson.AddField("id", room.RoomId);
                roomJson.AddField("name", options.Name);
                roomJson.AddField("ip", options.RoomIp);
                roomJson.AddField("port", options.RoomPort);
                roomJson.AddField("is_public", options.IsPublic);
                roomJson.AddField("players_count", onlineCount);
                roomJson.AddField("players_max", options.MaxPlayers);
                roomJson.AddField("password", options.Password);
                roomJson.AddField("region", options.Region);
                roomJson.AddField("custom_options", options.ExtraParameters.ToJson());

                info["properties"]["rooms"].Add(roomJson);
            }

            info["properties"].AddField("total_players", totalPlayers);

            return info;
        }

        /// <summary>
        /// Cleans up the list of unconfirmed accesses
        /// </summary>
        private void CleanUnconfirmedAccesses()
        {
            foreach (var room in roomsList.Values)
            {
                room.ClearTimedOutAccesses();
            }
        }

        private void CleanInvalidRooms()
        {
            List<RegisteredRoom> invalidRooms = new();

            foreach (var room in roomsList.Values)
            {
                if (!room.Peer.IsConnected)
                {
                    invalidRooms.Add(room);
                }
            }

            foreach (var room in invalidRooms)
            {
                logger.Warn($"Room {room.RoomId} peer is disconnected. Destroying invalid room");
                DestroyRoom(room);
            }
        }

        /// <summary>
        /// Returns true, if peer has permissions to register a game server
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        protected virtual bool HasRoomRegistrationPermissions(IPeer peer)
        {
            var extension = peer.GetExtension<SecurityInfoPeerExtension>();
            return extension != null &&
                   (extension.HasPermission(MstPermissionKeys.RoomServer) ||
                    extension.HasAccountPermission(MstPermissionLevels.Admin));
        }

        protected virtual void OnRoomRegistered(RegisteredRoom room) { }

        protected virtual void OnRoomDestroyed(RegisteredRoom room) { }

        /// <summary>
        /// Fired when registered room peer disconnected from master
        /// </summary>
        /// <param name="peer"></param>
        private void OnRegisteredPeerDisconnect(IPeer peer)
        {
            if (peer == null || !ownerRoomsByPeerId.TryRemove(peer.Id, out OwnerRoomsState ownerState))
                return;

            logger.Debug($"Client {peer.Id} was disconnected from server and it has registered rooms that also must be destroyed");

            List<RegisteredRoom> registeredRooms;

            lock (ownerState.Gate)
            {
                ownerState.IsActive = false;
                registeredRooms = ownerState.Rooms.Values.ToList();
                ownerState.Rooms.Clear();
            }

            foreach (var registeredRoom in registeredRooms)
                DestroyRoom(registeredRoom);
        }

        /// <summary>
        /// Registers a room to the server
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public virtual RegisteredRoom RegisterRoom(IPeer peer, RoomOptions options)
        {
            if (peer == null)
                throw new ArgumentNullException(nameof(peer));

            if (options == null)
                throw new ArgumentNullException(nameof(options));

            RegisteredRoom room;
            bool publishStateEvents;

            lock (runGate)
            {
                if (!acceptsRoomRegistrations)
                    return null;

                OwnerRoomsState ownerState = GetOrCreateOwnerRoomsState(peer);
                room = new RegisteredRoom(AllocateRoomId(), peer, options, true);

                lock (ownerState.Gate)
                {
                    if (!ownerState.IsActive || !peer.IsConnected)
                        return null;

                    room.SetDestroyOwner(DestroyRoom);
                    ownerState.Rooms[room.RoomId] = room;
                    roomsList[room.RoomId] = room;

                    if (!room.TryActivate(PublishRoomRegistered, out publishStateEvents))
                    {
                        ownerState.Rooms.TryRemove(room.RoomId, out _);
                        roomsList.TryRemove(room.RoomId, out _);
                        return null;
                    }
                }
            }

            if (publishStateEvents)
                room.PublishPendingStateEvents();

            return room;
        }

        private OwnerRoomsState GetOrCreateOwnerRoomsState(IPeer peer)
        {
            while (true)
            {
                if (ownerRoomsByPeerId.TryGetValue(peer.Id, out OwnerRoomsState existingState))
                    return existingState;

                var newState = new OwnerRoomsState(peer);

                if (!ownerRoomsByPeerId.TryAdd(peer.Id, newState))
                    continue;

                peer.SetProperty(MstPeerPropertyCodes.RegisteredRooms, newState.Rooms);
                peer.OnConnectionCloseEvent += OnRegisteredPeerDisconnect;

                if (!peer.IsConnected)
                    OnRegisteredPeerDisconnect(peer);

                return newState;
            }
        }

        /// <summary>
        /// Unregisters a room from a server
        /// </summary>
        /// <param name="room"></param>
        public virtual void DestroyRoom(RegisteredRoom room)
        {
            if (room == null ||
                !roomsList.TryRemove(room.RoomId, out RegisteredRoom removedRoom) ||
                !ReferenceEquals(room, removedRoom))
            {
                return;
            }

            var peer = room.Peer;

            if (peer != null && ownerRoomsByPeerId.TryGetValue(peer.Id, out OwnerRoomsState ownerState))
            {
                lock (ownerState.Gate)
                    ownerState.Rooms.TryRemove(room.RoomId, out _);
            }

            if (!room.TryDestroy(out _, PublishRoomDestroyed))
                return;

            logger.Debug($"Room {room.RoomId} has been successfully destroyed");
        }

        private void PublishRoomRegistered(RegisteredRoom room)
        {
            InvokeRoomEventSafely(OnRoomRegisteredEvent, room, nameof(OnRoomRegisteredEvent));

            try
            {
                OnRoomRegistered(room);
            }
            catch (Exception exception)
            {
                logger.Error($"Room {room.RoomId} registration hook failed: {exception}");
            }
        }

        private void PublishRoomDestroyed(RegisteredRoom room)
        {
            InvokeRoomEventSafely(OnRoomDestroyedEvent, room, nameof(OnRoomDestroyedEvent));

            try
            {
                OnRoomDestroyed(room);
            }
            catch (Exception exception)
            {
                logger.Error($"Room {room.RoomId} destruction hook failed: {exception}");
            }
        }

        private void InvokeRoomEventSafely(Action<RegisteredRoom> handlers, RegisteredRoom room, string eventName)
        {
            if (handlers == null)
                return;

            foreach (Action<RegisteredRoom> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(room);
                }
                catch (Exception exception)
                {
                    logger.Error($"{eventName} subscriber failed for room {room.RoomId}: {exception}");
                }
            }
        }

        /// <summary>
        /// There are times when you need to change registered room options. This method will help you :)
        /// </summary>
        /// <param name="room"></param>
        /// <param name="options"></param>
        public virtual void ChangeRoomOptions(RegisteredRoom room, RoomOptions options)
        {
            room?.TryChangeOptions(options);
        }

        /// <summary>
        /// Returns list of all public rooms by given filter
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="filters"></param>
        /// <returns></returns>
        public virtual IEnumerable<GameInfoPacket> GetPublicGames(IPeer peer, MstProperties filters)
        {
            var games = new List<GameInfoPacket>();
            bool filterByRoomId = filters != null && filters.Has(MstParamKeys.ROOM_ID);
            int requestedRoomId = filterByRoomId ? filters.AsInt(MstParamKeys.ROOM_ID) : -1;

            foreach (RegisteredRoom room in roomsList.Values)
            {
                if (room == null ||
                    !room.TryGetSnapshot(out RoomOptions options,
                        out IReadOnlyDictionary<int, IPeer> roomPlayers, out int onlineCount))
                {
                    continue;
                }

                if (filterByRoomId)
                {
                    if (room.RoomId != requestedRoomId ||
                        (!options.IsPublic && (peer == null || !roomPlayers.ContainsKey(peer.Id))))
                    {
                        continue;
                    }
                }
                else if (!options.IsPublic)
                {
                    continue;
                }

                var game = new GameInfoPacket
                {
                    Id = room.RoomId,
                    Address = options.RoomIp + ":" + options.RoomPort,
                    MaxPlayers = options.MaxPlayers,
                    Name = options.Name,
                    OnlinePlayers = onlineCount,
                    IsPasswordProtected = !string.IsNullOrEmpty(options.Password),
                    Type = GameInfoType.Room,
                    Region = options.Region,
                    Properties = GetPublicRoomOptions(peer, room, filters, options)
                };

                var players = new List<string>();

                foreach (IPeer roomPlayer in roomPlayers.Values.Where(player => player != null))
                {
                    if (!roomPlayer.HasExtension<IUserPeerExtension>())
                        continue;

                    var userExtension = roomPlayer.GetExtension<IUserPeerExtension>();

                    if (userExtension == null)
                    {
                        logger.Warn($"Peer {roomPlayer.Id} in room {room.RoomId} reported user extension but returned null");
                        continue;
                    }

                    players.Add(userExtension.Username);
                }

                game.OnlinePlayersList = players;
                games.Add(game);
            }

            return games;
        }

        /// <summary>
        /// Returns public room properties for callers that do not already own an options snapshot.
        /// Base room listings use the four-argument overload instead.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="room"></param>
        /// <param name="playerFilters"></param>
        /// <returns></returns>
        public virtual MstProperties GetPublicRoomOptions(IPeer player, RegisteredRoom room, MstProperties playerFilters)
        {
            if (room == null || !room.TryGetSnapshot(out RoomOptions options, out _, out _))
                return new MstProperties();

            return GetPublicRoomOptions(player, room, playerFilters, options);
        }

        /// <summary>
        /// Returns public room properties from the same options snapshot used to build a game packet.
        /// MST5 room-listing customizations must override this overload; overriding only the
        /// three-argument compatibility overload does not affect <see cref="GetPublicGames"/>.
        /// </summary>
        public virtual MstProperties GetPublicRoomOptions(IPeer player, RegisteredRoom room,
            MstProperties playerFilters, RoomOptions optionsSnapshot)
        {
            return new MstProperties(optionsSnapshot?.ExtraParameters);
        }

        /// <summary>
        /// Returns room by given id
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        public RegisteredRoom GetRoomById(int roomId)
        {
            roomsList.TryGetValue(roomId, out RegisteredRoom r);
            return r != null && r.IsActive ? r : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="room"></param>
        /// <returns></returns>
        public bool TryGetRoomById(int roomId, out RegisteredRoom room)
        {
            room = GetRoomById(roomId);
            return room != null && room.IsActive;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionName"></param>
        /// <returns></returns>
        public IEnumerable<RegisteredRoom> GetRoomsByRegion(string regionName)
        {
            return GetAllRooms()
                .Where(room => room.TryGetSnapshot(out RoomOptions options, out _, out _) &&
                               options.Region == regionName)
                .ToList();
        }

        /// <summary>
        /// Returns the list of all registered rooms
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RegisteredRoom> GetAllRooms()
        {
            return roomsList.Values.Where(room => room != null && room.IsActive).ToList();
        }

        /// <summary>
        /// Get room players list
        /// </summary>
        /// <param name="roomId"></param>
        public IEnumerable<IPeer> GetPlayersOfRoom(int roomId)
        {
            var r = GetRoomById(roomId);

            if (r != null)
            {
                return r.GetPlayersSnapshot().Values.ToList();
            }
            else
            {
                logger.Warn($"Room {roomId} not found");
                return Enumerable.Empty<IPeer>();
            }
        }

        /// <summary>
        /// Notifies the active room process that an account connected to it was blocked.
        /// </summary>
        /// <returns><c>true</c> when the notification was sent to an active room.</returns>
        public bool TryNotifyAccountBlocked(IUserPeerExtension user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.UserId) || !user.HasJoinedRoom())
                return false;

            if (!TryGetRoomById(user.JoinedRoomID, out RegisteredRoom room) ||
                room.Peer == null ||
                !room.Peer.IsConnected)
            {
                return false;
            }

            try
            {
                room.Peer.SendMessage(MstOpCodes.AccountBlocked, user.UserId);
                return true;
            }
            catch (Exception exception)
            {
                logger.Error(
                    $"Failed to notify room about blocked account. AccountId={user.UserId}, RoomId={user.JoinedRoomID}, error={exception}");
                return false;
            }
        }

        /// <summary>
        /// Asks the active room to persist and release the exact session represented by
        /// <paramref name="user"/>. Missing or disconnected rooms are treated as stale
        /// membership and cleaned locally.
        /// </summary>
        /// <param name="user">Current authenticated session that may own a room player.</param>
        /// <param name="cancellationToken">Cancels waiting for the room response.</param>
        /// <returns><c>true</c> when no active room remains responsible for the session.</returns>
        public async Task<bool> ReleasePlayerSessionAsync(
            IUserPeerExtension user,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (user == null || string.IsNullOrWhiteSpace(user.UserId) || !user.HasJoinedRoom())
                return true;

            int roomId = user.JoinedRoomID;

            if (!roomsList.TryGetValue(roomId, out RegisteredRoom room) ||
                room == null ||
                !room.IsActive)
            {
                ClearStaleRoomMembership(user, roomId);
                return true;
            }

            if (room.Peer == null || !room.Peer.IsConnected)
            {
                DestroyRoom(room);
                ClearStaleRoomMembership(user, roomId);
                return true;
            }

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var packet = new RoomPlayerSessionPacket
            {
                AccountId = user.UserId,
                MasterPeerId = user.Peer.Id
            };

            try
            {
                room.Peer.SendMessage(
                    MstOpCodes.ReleaseRoomPlayerSessionRequest,
                    packet,
                    (status, _) => completion.TrySetResult(status == ResponseStatus.Success),
                    Math.Max(1, playerSessionReleaseTimeoutSeconds));
            }
            catch (Exception exception)
            {
                logger.Error(
                    $"Failed to request room player session release. " +
                    $"AccountId={user.UserId}, PeerId={user.Peer.Id}, RoomId={roomId}, error={exception}");
                return false;
            }

            using (cancellationToken.Register(() => completion.TrySetCanceled()))
            {
                bool released = await completion.Task;

                if (user.JoinedRoomID != roomId)
                    return true;

                logger.Warn(
                    $"Room did not confirm removal of the player session. " +
                    $"AccountId={user.UserId}, PeerId={user.Peer.Id}, RoomId={roomId}, " +
                    $"ResponseReceived={released}");
                return false;
            }
        }

        private static void ClearStaleRoomMembership(IUserPeerExtension user, int roomId)
        {
            if (user != null && user.JoinedRoomID == roomId)
                user.JoinedRoomID = -1;
        }

        #region Message Handlers

        protected virtual Task RegisterRoomRequestHandler(IIncomingMessage message)
        {
            try
            {
                logger.Debug($"Client {message.Peer.Id} requested to register new room server");

                if (!HasRoomRegistrationPermissions(message.Peer))
                {
                    logger.Debug($"But it has no permission");
                    message.RespondError(ResponseStatus.Unauthorized, MstErrorCodes.PERMISSION_DENIED);
                    return Task.CompletedTask;
                }

                var options = message.AsPacket<RoomOptions>();
                var room = RegisterRoom(message.Peer, options);

                if (room == null)
                {
                    if (!message.Peer.IsConnected)
                    {
                        message.RespondError(ResponseStatus.NotConnected,
                            MstErrorCodes.ROOM_REGISTRATION_DISCONNECTED);
                    }
                    else
                    {
                        message.RespondError(ResponseStatus.ServiceUnavailable,
                            MstErrorCodes.ROOM_REGISTRATION_UNAVAILABLE);
                    }

                    return Task.CompletedTask;
                }

                logger.Debug($"Room {room.RoomId} has been successfully registered with options: {options}");

                // Respond with a room id
                message.Respond(room.RoomId, ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return Task.FromException(ex);
            }
        }

        protected virtual Task DestroyRoomRequestHandler(IIncomingMessage message)
        {
            try
            {
                var roomId = message.AsInt();

                logger.Debug($"Client {message.Peer.Id} requested to destroy room server with id {roomId}");

                if (!TryGetRoomById(roomId, out RegisteredRoom room))
                {
                    logger.Warn($"But this room does not exist");
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.ROOM_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (message.Peer != room.Peer)
                {
                    logger.Warn($"But it is not the creator of the room");
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.ROOM_OWNER_REQUIRED);
                    return Task.CompletedTask;
                }

                DestroyRoom(room);
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return Task.FromException(ex);
            }
        }

        protected virtual Task ValidateRoomAccessRequestHandler(IIncomingMessage message)
        {
            try
            {
                // Parse message
                var data = message.AsPacket<RoomAccessValidatePacket>();

                // Trying to find room in list of registered
                if (!TryGetRoomById(data.RoomId, out RegisteredRoom room))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.ROOM_NOT_FOUND);
                    return Task.CompletedTask;
                }

                // if this message is not received from owner of room
                if (message.Peer != room.Peer)
                {
                    // Wrong peer of room registrar
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.ROOM_REGISTRAR_REQUIRED);
                    return Task.CompletedTask;
                }

                // Trying to validate room access token
                if (!room.ValidateAccess(data.Token, out IPeer playerPeer))
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.ROOM_ACCESS_TOKEN_INVALID);
                    return Task.CompletedTask;
                }

                var packet = new UsernameAndPeerIdPacket()
                {
                    PeerId = playerPeer.Id
                };

                // Add username if available
                var userExt = playerPeer.GetExtension<IUserPeerExtension>();
                if (userExt != null)
                {
                    packet.Username = userExt.Username ?? "";
                }

                // Respond with success and player's peer id
                message.Respond(packet, ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return Task.FromException(ex);
            }
        }

        protected virtual Task SaveRoomOptionsRequestHandler(IIncomingMessage message)
        {
            try
            {
                var data = message.AsPacket<SaveRoomOptionsPacket>();

                if (!TryGetRoomById(data.RoomId, out RegisteredRoom room))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.ROOM_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (message.Peer != room.Peer)
                {
                    // Wrong peer unregistering the room
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.ROOM_OWNER_REQUIRED);
                    return Task.CompletedTask;
                }

                if (!room.TryChangeOptions(data.Options))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.ROOM_NOT_FOUND);
                    return Task.CompletedTask;
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return Task.FromException(ex);
            }
        }

        protected virtual async Task GetRoomAccessRequestHandler(IIncomingMessage message,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var data = message.AsPacket<RoomAccessRequestPacket>();

                // Let's find a room by Id which the player wants to join
                if (!TryGetRoomById(data.RoomId, out RegisteredRoom room))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.ROOM_NOT_FOUND);
                    return;
                }

                if (!room.TryGetSnapshot(out RoomOptions roomOptions, out _, out _))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.ROOM_NOT_FOUND);
                    return;
                }

                // If room requires the password and given password is not valid
                if (!string.IsNullOrEmpty(roomOptions.Password) && roomOptions.Password != data.Password)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.ROOM_PASSWORD_INVALID);
                    return;
                }

                // Send room access request to peer who owns it
                await room.GetAccessResponseAsync(message.Peer, data.CustomOptions, (packet, status, errorPayload) =>
                {
                    if (packet == null)
                    {
                        ResponseStatus errorStatus = status == ResponseStatus.Success
                            ? ResponseStatus.Invalid
                            : status;

                        if (errorPayload == null || errorPayload.Length == 0)
                        {
                            message.RespondError(errorStatus, MstErrorCodes.RESPONSE_INVALID);
                            return;
                        }

                        message.Respond(errorPayload, errorStatus);
                        return;
                    }

                    message.Respond(packet, ResponseStatus.Success);
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        protected virtual Task PlayerLeftRoomRequestHandler(IIncomingMessage message)
        {
            try
            {
                var data = message.AsPacket<PlayerLeftRoomPacket>();

                if (!TryGetRoomById(data.RoomId, out RegisteredRoom room))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.ROOM_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (message.Peer != room.Peer)
                {
                    // Wrong peer unregistering the room
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.ROOM_OWNER_REQUIRED);
                    return Task.CompletedTask;
                }

                room.RemovePlayer(data.PeerId);
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return Task.FromException(ex);
            }
        }

        #endregion
    }
}
