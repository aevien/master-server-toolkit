using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents a registered room on the master server.
    /// </summary>
    public class RegisteredRoom
    {
        public const string RoomDestroyedError = "Room is destroyed";

        public delegate void GetAccessCallback(RoomAccessPacket access, string error);
        internal delegate void GetAccessResponseCallback(RoomAccessPacket access, ResponseStatus status,
            byte[] errorPayload);

        private enum LifecycleState
        {
            Registering,
            Active,
            Destroyed
        }

        private readonly object stateGate = new object();
        private readonly Dictionary<int, RoomAccessPacket> accessesInUse;
        private readonly Dictionary<string, RoomAccessData> unconfirmedAccesses;
        private readonly Dictionary<int, object> requestsInProgress;
        private readonly Dictionary<int, IPeer> players;
        private readonly Queue<Action> pendingStateEvents;

        private LifecycleState lifecycleState;
        private RoomOptions options;
        private IReadOnlyDictionary<int, IPeer> playersSnapshot;
        private bool isPublishingStateEvents;
        private Action<IPeer> onPlayerJoinedEvent;
        private Action<IPeer> onPlayerLeftEvent;
        private Action<RegisteredRoom> onDestroyedEvent;
        private Action<RegisteredRoom> destroyOwner;

        /// <summary>
        /// Gets an independent copy of the latest room options.
        /// </summary>
        public RoomOptions GetOptionsSnapshot()
        {
            RoomOptions currentOptions;

            lock (stateGate)
                currentOptions = options;

            return currentOptions.Clone();
        }

        /// <summary>
        /// Gets the room identifier.
        /// </summary>
        public int RoomId { get; }

        /// <summary>
        /// Gets the peer that registered this room.
        /// </summary>
        public IPeer Peer { get; }

        /// <summary>
        /// Gets the number of consumed room accesses.
        /// </summary>
        public int OnlineCount
        {
            get
            {
                lock (stateGate)
                    return accessesInUse.Count;
            }
        }

        /// <summary>
        /// Gets whether the room still accepts state mutations.
        /// </summary>
        public bool IsActive
        {
            get
            {
                lock (stateGate)
                    return lifecycleState == LifecycleState.Active;
            }
        }

        public event Action<IPeer> OnPlayerJoinedEvent
        {
            add
            {
                lock (stateGate)
                {
                    if (lifecycleState == LifecycleState.Active)
                        onPlayerJoinedEvent += value;
                }
            }
            remove
            {
                lock (stateGate)
                    onPlayerJoinedEvent -= value;
            }
        }

        public event Action<IPeer> OnPlayerLeftEvent
        {
            add
            {
                lock (stateGate)
                {
                    if (lifecycleState == LifecycleState.Active)
                        onPlayerLeftEvent += value;
                }
            }
            remove
            {
                lock (stateGate)
                    onPlayerLeftEvent -= value;
            }
        }

        public event Action<RegisteredRoom> OnDestroyedEvent
        {
            add => TryAttachDestroyedListener(value);
            remove => DetachDestroyedListener(value);
        }

        public RegisteredRoom(int roomId, IPeer peer, RoomOptions options)
            : this(roomId, peer, options, false)
        {
        }

        internal RegisteredRoom(int roomId, IPeer peer, RoomOptions options, bool deferActivation)
        {
            RoomId = roomId;
            Peer = peer;
            this.options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));

            requestsInProgress = new Dictionary<int, object>();
            unconfirmedAccesses = new Dictionary<string, RoomAccessData>();
            accessesInUse = new Dictionary<int, RoomAccessPacket>();
            players = new Dictionary<int, IPeer>();
            pendingStateEvents = new Queue<Action>();
            lifecycleState = deferActivation ? LifecycleState.Registering : LifecycleState.Active;
        }

        /// <summary>
        /// Replaces room options while preserving the existing public API.
        /// </summary>
        public void ChangeOptions(RoomOptions newOptions)
        {
            TryChangeOptions(newOptions);
        }

        /// <summary>
        /// Replaces room options only while the room is active.
        /// </summary>
        public bool TryChangeOptions(RoomOptions newOptions)
        {
            if (newOptions == null)
                throw new ArgumentNullException(nameof(newOptions));

            RoomOptions optionsCopy = newOptions.Clone();

            lock (stateGate)
            {
                if (lifecycleState != LifecycleState.Active)
                    return false;

                options = optionsCopy;
                return true;
            }
        }

        /// <summary>
        /// Gets an immutable player snapshot.
        /// </summary>
        public IReadOnlyDictionary<int, IPeer> GetPlayersSnapshot()
        {
            lock (stateGate)
                return CreatePlayersSnapshotLocked();
        }

        /// <summary>
        /// Gets room options, players and online count from one synchronized state observation.
        /// </summary>
        public bool TryGetSnapshot(out RoomOptions roomOptions,
            out IReadOnlyDictionary<int, IPeer> playerSnapshot, out int onlineCount)
        {
            RoomOptions currentOptions;
            bool isActive;

            lock (stateGate)
            {
                currentOptions = options;
                playerSnapshot = CreatePlayersSnapshotLocked();
                onlineCount = accessesInUse.Count;
                isActive = lifecycleState == LifecycleState.Active;
            }

            roomOptions = currentOptions.Clone();
            return isActive;
        }

        /// <summary>
        /// Attaches a destruction listener only if the room is still active.
        /// </summary>
        public bool TryAttachDestroyedListener(Action<RegisteredRoom> listener)
        {
            if (listener == null)
                return false;

            lock (stateGate)
            {
                if (lifecycleState != LifecycleState.Active)
                    return false;

                onDestroyedEvent += listener;
                return true;
            }
        }

        /// <summary>
        /// Removes a destruction listener.
        /// </summary>
        public void DetachDestroyedListener(Action<RegisteredRoom> listener)
        {
            if (listener == null)
                return;

            lock (stateGate)
                onDestroyedEvent -= listener;
        }

        public void GetAccess(IPeer peer, GetAccessCallback callback)
        {
            GetAccess(peer, new MstProperties(), callback);
        }

        public void GetAccess(IPeer peer, MstProperties customOptions, GetAccessCallback callback)
        {
            GetAccessResponse(peer, customOptions, (access, status, errorPayload) =>
                callback?.Invoke(access, GetLegacyAccessError(status, errorPayload)));
        }

        /// <summary>
        /// Sends a request to the room and completes after its terminal callback.
        /// Cancellation completes the caller immediately. A pending ACK may only clear its request marker.
        /// </summary>
        public Task GetAccessAsync(IPeer peer, MstProperties customOptions, GetAccessCallback callback,
            CancellationToken cancellationToken)
        {
            return GetAccessResponseAsync(peer, customOptions, (access, status, errorPayload) =>
                callback?.Invoke(access, GetLegacyAccessError(status, errorPayload)), cancellationToken);
        }

        internal void GetAccessResponse(IPeer peer, MstProperties customOptions, GetAccessResponseCallback callback)
        {
            GetAccessCore(peer, customOptions, callback, new object(), CancellationToken.None);
        }

        internal async Task GetAccessResponseAsync(IPeer peer, MstProperties customOptions,
            GetAccessResponseCallback callback, CancellationToken cancellationToken)
        {
            if (peer == null)
                throw new ArgumentNullException(nameof(peer));

            customOptions ??= new MstProperties();

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reservationIdentity = new object();
            int completionState = 0;
            int reservationState = 0;

            using (cancellationToken.Register(() =>
            {
                if (Interlocked.CompareExchange(ref completionState, 1, 0) == 0)
                {
                    if (Interlocked.Exchange(ref reservationState, 2) == 1)
                        ReleaseRequestReservation(peer.Id, reservationIdentity);

                    completion.TrySetCanceled();
                }
            }))
            {
                try
                {
                    GetAccessCore(peer, customOptions, (access, status, errorPayload) =>
                    {
                        try
                        {
                            callback?.Invoke(access, status, errorPayload);
                            completion.TrySetResult(true);
                        }
                        catch (Exception exception)
                        {
                            completion.TrySetException(exception);
                        }
                    }, reservationIdentity, cancellationToken,
                    () => Interlocked.CompareExchange(ref completionState, 2, 0) == 0,
                    exception => completion.TrySetException(exception),
                    () => Interlocked.CompareExchange(ref reservationState, 1, 0) == 0);
                }
                catch (OperationCanceledException)
                {
                    if (Interlocked.CompareExchange(ref completionState, 1, 0) == 0)
                        completion.TrySetCanceled();
                }
                catch (Exception exception)
                {
                    if (Volatile.Read(ref completionState) == 2 ||
                        Interlocked.CompareExchange(ref completionState, 2, 0) == 0)
                    {
                        completion.TrySetException(exception);
                    }
                }

                await completion.Task;
            }
        }

        private void GetAccessCore(IPeer peer, MstProperties customOptions, GetAccessResponseCallback callback,
            object reservationIdentity, CancellationToken cancellationToken, Func<bool> tryBeginAckCompletion = null,
            Action<Exception> ackExceptionCallback = null, Func<bool> tryActivateReservation = null)
        {
            if (peer == null)
                throw new ArgumentNullException(nameof(peer));

            if (reservationIdentity == null)
                throw new ArgumentNullException(nameof(reservationIdentity));

            customOptions ??= new MstProperties();
            cancellationToken.ThrowIfCancellationRequested();

            bool TryBeginCompletion()
            {
                return tryBeginAckCompletion == null || tryBeginAckCompletion.Invoke();
            }

            RoomAccessData currentAccess = null;
            RoomAccessPacket immediateAccess = null;
            ResponseStatus immediateStatus = ResponseStatus.Success;
            string immediateErrorCode = null;
            bool shouldRequestAccess = false;
            bool reservationCanceled = false;

            lock (stateGate)
            {
                if (lifecycleState != LifecycleState.Active)
                {
                    immediateStatus = ResponseStatus.NotFound;
                    immediateErrorCode = MstErrorCodes.ROOM_DESTROYED;
                }
                else if (requestsInProgress.ContainsKey(peer.Id))
                {
                    immediateStatus = ResponseStatus.Conflict;
                    immediateErrorCode = MstErrorCodes.ROOM_ACCESS_REQUEST_ALREADY_ACTIVE;
                }
                else if (players.ContainsKey(peer.Id))
                {
                    immediateStatus = ResponseStatus.Conflict;
                    immediateErrorCode = MstErrorCodes.ROOM_ALREADY_JOINED;
                }
                else
                {
                    foreach (RoomAccessData access in unconfirmedAccesses.Values)
                    {
                        if (!ReferenceEquals(access.Peer, peer))
                            continue;

                        currentAccess = access;
                        break;
                    }

                    if (currentAccess == null && options.MaxPlayers > 0)
                    {
                        int playerSlotsTaken = requestsInProgress.Count + accessesInUse.Count + unconfirmedAccesses.Count;

                        if (playerSlotsTaken >= options.MaxPlayers)
                        {
                            immediateStatus = ResponseStatus.Conflict;
                            immediateErrorCode = MstErrorCodes.ROOM_FULL;
                        }
                    }

                    if (currentAccess == null && immediateErrorCode == null)
                    {
                        if (tryActivateReservation != null && !tryActivateReservation.Invoke())
                        {
                            reservationCanceled = true;
                        }
                        else
                        {
                            requestsInProgress.Add(peer.Id, reservationIdentity);
                            shouldRequestAccess = true;
                        }
                    }
                }
            }

            if (reservationCanceled)
                throw new OperationCanceledException(cancellationToken);

            if (!shouldRequestAccess)
            {
                if (!TryBeginCompletion())
                    return;

                lock (stateGate)
                {
                    if (lifecycleState != LifecycleState.Active)
                    {
                        immediateAccess = null;
                        immediateStatus = ResponseStatus.NotFound;
                        immediateErrorCode = MstErrorCodes.ROOM_DESTROYED;
                    }
                    else if (currentAccess != null)
                    {
                        string token = currentAccess.Access.Token;

                        if (unconfirmedAccesses.TryGetValue(token, out RoomAccessData activeAccess) &&
                            ReferenceEquals(activeAccess, currentAccess))
                        {
                            activeAccess.Timeout = DateTime.Now.AddSeconds(options.AccessTimeoutPeriod);
                            immediateAccess = activeAccess.Access;
                            immediateStatus = ResponseStatus.Success;
                            immediateErrorCode = null;
                        }
                        else
                        {
                            immediateAccess = null;
                            immediateStatus = ResponseStatus.Conflict;
                            immediateErrorCode = MstErrorCodes.ROOM_ACCESS_UNAVAILABLE;
                        }
                    }
                }

                callback?.Invoke(immediateAccess, immediateStatus, CreateErrorPayload(immediateErrorCode));

                return;
            }

            var provideRoomAccessCheckPacket = new ProvideRoomAccessCheckPacket
            {
                PeerId = peer.Id,
                RoomId = RoomId,
                CustomOptions = customOptions
            };

            var userPeerExtension = peer.GetExtension<IUserPeerExtension>();

            if (userPeerExtension != null && !string.IsNullOrEmpty(userPeerExtension.Username))
                provideRoomAccessCheckPacket.Username = userPeerExtension.Username;

            try
            {
                Peer.SendMessage(MstOpCodes.ProvideRoomAccessCheck, provideRoomAccessCheckPacket, (status, response) =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            ReleaseRequestReservation(peer.Id, reservationIdentity);
                            return;
                        }

                        if (!TryBeginCompletion())
                        {
                            ReleaseRequestReservation(peer.Id, reservationIdentity);
                            return;
                        }

                        byte[] responseErrorPayload = status == ResponseStatus.Success
                            ? null
                            : response?.AsBytes();
                        ResponseStatus completionStatus = status;
                        byte[] completionErrorPayload = null;

                        lock (stateGate)
                        {
                            if (lifecycleState != LifecycleState.Active)
                            {
                                ReleaseRequestReservationLocked(peer.Id, reservationIdentity);
                                completionStatus = ResponseStatus.NotFound;
                                completionErrorPayload = CreateErrorPayload(MstErrorCodes.ROOM_DESTROYED);
                            }
                            else if (status != ResponseStatus.Success)
                            {
                                ReleaseRequestReservationLocked(peer.Id, reservationIdentity);
                                completionErrorPayload = responseErrorPayload == null || responseErrorPayload.Length == 0
                                    ? CreateErrorPayload(MstErrorCodes.RESPONSE_INVALID)
                                    : responseErrorPayload;
                            }
                        }

                        if (completionErrorPayload != null)
                        {
                            callback?.Invoke(null, completionStatus, completionErrorPayload);
                            return;
                        }

                        RoomAccessPacket accessData = response.AsPacket<RoomAccessPacket>();

                        lock (stateGate)
                        {
                            if (lifecycleState != LifecycleState.Active)
                            {
                                ReleaseRequestReservationLocked(peer.Id, reservationIdentity);
                                completionStatus = ResponseStatus.NotFound;
                                completionErrorPayload = CreateErrorPayload(MstErrorCodes.ROOM_DESTROYED);
                            }
                            else if (!ReleaseRequestReservationLocked(peer.Id, reservationIdentity))
                            {
                                completionStatus = ResponseStatus.Conflict;
                                completionErrorPayload = CreateErrorPayload(MstErrorCodes.ROOM_ACCESS_REQUEST_INACTIVE);
                            }
                            else if (unconfirmedAccesses.ContainsKey(accessData.Token))
                            {
                                completionStatus = ResponseStatus.Conflict;
                                completionErrorPayload = CreateErrorPayload(MstErrorCodes.ROOM_ACCESS_TOKEN_IN_USE);
                            }
                            else
                            {
                                unconfirmedAccesses.Add(accessData.Token, new RoomAccessData
                                {
                                    Access = accessData,
                                    Peer = peer,
                                    Timeout = DateTime.Now.AddSeconds(options.AccessTimeoutPeriod)
                                });
                            }
                        }

                        if (completionErrorPayload != null)
                        {
                            callback?.Invoke(null, completionStatus, completionErrorPayload);
                            return;
                        }

                        callback?.Invoke(accessData, ResponseStatus.Success, null);
                    }
                    catch (Exception exception)
                    {
                        ReleaseRequestReservation(peer.Id, reservationIdentity);

                        if (ackExceptionCallback != null)
                        {
                            ackExceptionCallback.Invoke(exception);
                            return;
                        }

                        throw;
                    }
                });
            }
            catch
            {
                ReleaseRequestReservation(peer.Id, reservationIdentity);
                throw;
            }
        }

        /// <summary>
        /// Atomically consumes an access token and adds its player to this room.
        /// </summary>
        public bool ValidateAccess(string token, out IPeer peer)
        {
            peer = null;
            bool publishStateEvents;

            lock (stateGate)
            {
                if (lifecycleState != LifecycleState.Active ||
                    !unconfirmedAccesses.TryGetValue(token, out RoomAccessData data))
                {
                    return false;
                }

                unconfirmedAccesses.Remove(token);

                if (!data.Peer.IsConnected ||
                    players.ContainsKey(data.Peer.Id) ||
                    accessesInUse.ContainsKey(data.Peer.Id))
                {
                    return false;
                }

                IUserPeerExtension user = data.Peer.GetExtension<IUserPeerExtension>();

                if (user == null)
                    return false;

                accessesInUse.Add(data.Peer.Id, data.Access);
                players.Add(data.Peer.Id, data.Peer);
                playersSnapshot = null;
                user.JoinedRoomID = RoomId;

                peer = data.Peer;
                Action<IPeer> joinedHandler = onPlayerJoinedEvent;
                publishStateEvents = QueueStateEventLocked(joinedHandler == null
                    ? null
                    : () => joinedHandler(data.Peer));
            }

            if (publishStateEvents)
                PublishStateEvents();

            return true;
        }

        /// <summary>
        /// Atomically removes a player from this room.
        /// </summary>
        public void RemovePlayer(int peerId)
        {
            IPeer playerPeer;
            bool publishStateEvents;

            lock (stateGate)
            {
                if (lifecycleState != LifecycleState.Active)
                    return;

                accessesInUse.Remove(peerId);

                if (!players.TryGetValue(peerId, out playerPeer))
                    return;

                players.Remove(peerId);
                playersSnapshot = null;

                if (playerPeer.TryGetExtension(out IUserPeerExtension user) && user.JoinedRoomID == RoomId)
                    user.JoinedRoomID = -1;

                Action<IPeer> leftHandler = onPlayerLeftEvent;
                publishStateEvents = QueueStateEventLocked(leftHandler == null
                    ? null
                    : () => leftHandler(playerPeer));
            }

            if (publishStateEvents)
                PublishStateEvents();
        }

        /// <summary>
        /// Removes expired unconfirmed accesses under the room state gate.
        /// </summary>
        public void ClearTimedOutAccesses()
        {
            DateTime currentTime = DateTime.Now;

            lock (stateGate)
            {
                if (lifecycleState != LifecycleState.Active || unconfirmedAccesses.Count == 0)
                    return;

                List<string> timedOutTokens = null;

                foreach (KeyValuePair<string, RoomAccessData> pair in unconfirmedAccesses)
                {
                    if (pair.Value.Timeout >= currentTime)
                        continue;

                    timedOutTokens ??= new List<string>();
                    timedOutTokens.Add(pair.Key);
                }

                if (timedOutTokens == null)
                    return;

                foreach (string timedOutToken in timedOutTokens)
                    unconfirmedAccesses.Remove(timedOutToken);
            }
        }

        public void Destroy()
        {
            Action<RegisteredRoom> owner;

            lock (stateGate)
                owner = destroyOwner;

            if (owner != null)
            {
                owner.Invoke(this);
                return;
            }

            TryDestroy(out _);
        }

        internal void SetDestroyOwner(Action<RegisteredRoom> owner)
        {
            lock (stateGate)
            {
                if (lifecycleState != LifecycleState.Destroyed)
                    destroyOwner = owner;
            }
        }

        /// <summary>
        /// Activates a room created in the registering state and queues its registration publication.
        /// The caller owns publishing so callbacks can run after its registry locks are released.
        /// </summary>
        internal bool TryActivate(Action<RegisteredRoom> activatedCallback, out bool publishStateEvents)
        {
            lock (stateGate)
            {
                if (lifecycleState != LifecycleState.Registering)
                {
                    publishStateEvents = false;
                    return false;
                }

                lifecycleState = LifecycleState.Active;
                publishStateEvents = QueueStateEventLocked(activatedCallback == null
                    ? null
                    : () => activatedCallback(this));
                return true;
            }
        }

        internal void PublishPendingStateEvents()
        {
            PublishStateEvents();
        }

        /// <summary>
        /// Atomically transitions this room to the destroyed state and returns its final player snapshot.
        /// </summary>
        internal bool TryDestroy(out IReadOnlyDictionary<int, IPeer> playerSnapshot)
        {
            return TryDestroy(out playerSnapshot, null);
        }

        /// <summary>
        /// Atomically destroys the room and queues owner publication after room-local callbacks.
        /// </summary>
        internal bool TryDestroy(out IReadOnlyDictionary<int, IPeer> playerSnapshot,
            Action<RegisteredRoom> destroyedCallback)
        {
            bool publishStateEvents;

            lock (stateGate)
            {
                if (lifecycleState == LifecycleState.Destroyed)
                {
                    playerSnapshot = CreatePlayersSnapshotLocked();
                    return false;
                }

                lifecycleState = LifecycleState.Destroyed;
                playerSnapshot = CreatePlayersSnapshotLocked();
                Action<RegisteredRoom> destroyedHandler = onDestroyedEvent;

                foreach (IPeer playerPeer in players.Values)
                {
                    if (playerPeer != null &&
                        playerPeer.TryGetExtension(out IUserPeerExtension user) &&
                        user.JoinedRoomID == RoomId)
                    {
                        user.JoinedRoomID = -1;
                    }
                }

                requestsInProgress.Clear();
                unconfirmedAccesses.Clear();
                accessesInUse.Clear();
                players.Clear();
                playersSnapshot = null;

                onPlayerJoinedEvent = null;
                onPlayerLeftEvent = null;
                onDestroyedEvent = null;
                destroyOwner = null;

                publishStateEvents = QueueStateEventLocked(destroyedHandler == null
                    ? null
                    : () => destroyedHandler(this));

                if (QueueStateEventLocked(destroyedCallback == null
                    ? null
                    : () => destroyedCallback(this)))
                {
                    publishStateEvents = true;
                }
            }

            if (publishStateEvents)
                PublishStateEvents();

            return true;
        }

        private void ReleaseRequestReservation(int peerId, object reservationIdentity)
        {
            lock (stateGate)
                ReleaseRequestReservationLocked(peerId, reservationIdentity);
        }

        private bool ReleaseRequestReservationLocked(int peerId, object reservationIdentity)
        {
            if (!requestsInProgress.TryGetValue(peerId, out object activeReservation) ||
                !ReferenceEquals(activeReservation, reservationIdentity))
            {
                return false;
            }

            return requestsInProgress.Remove(peerId);
        }

        private IReadOnlyDictionary<int, IPeer> CreatePlayersSnapshotLocked()
        {
            return playersSnapshot ??=
                new ReadOnlyDictionary<int, IPeer>(new Dictionary<int, IPeer>(players));
        }

        private byte[] CreateErrorPayload(string errorCode)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
                return null;

            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, errorCode);
            properties.Set(MstErrorPropertyKeys.ROOM_ID, RoomId);
            return properties.ToBytes();
        }

        private static string GetLegacyAccessError(ResponseStatus status, byte[] errorPayload)
        {
            if (status == ResponseStatus.Success)
                return null;

            string code = null;

            if (errorPayload != null && errorPayload.Length > 0)
            {
                try
                {
                    code = MstProperties.FromBytes(errorPayload)
                        .AsString(MstErrorPropertyKeys.CODE);
                }
                catch
                {
                    // Legacy callbacks expose an English fallback; socket forwarding keeps the raw payload intact.
                }
            }

            switch (code)
            {
                case MstErrorCodes.ROOM_DESTROYED:
                    return RoomDestroyedError;
                case MstErrorCodes.ROOM_ACCESS_REQUEST_ALREADY_ACTIVE:
                    return "You've already requested an access to this room";
                case MstErrorCodes.ROOM_ALREADY_JOINED:
                    return "You are already in this room";
                case MstErrorCodes.ROOM_FULL:
                    return "Room is already full";
                case MstErrorCodes.ROOM_ACCESS_UNAVAILABLE:
                    return "Room access is no longer available";
                case MstErrorCodes.ROOM_ACCESS_REQUEST_INACTIVE:
                    return "Room access request is no longer active";
                case MstErrorCodes.ROOM_ACCESS_TOKEN_IN_USE:
                    return "Room access token is already in use";
                default:
                    return string.IsNullOrWhiteSpace(code) ? status.ToString() : code;
            }
        }

        private bool QueueStateEventLocked(Action stateEvent)
        {
            if (stateEvent == null)
                return false;

            pendingStateEvents.Enqueue(stateEvent);
            return true;
        }

        private void PublishStateEvents()
        {
            lock (stateGate)
            {
                if (isPublishingStateEvents || pendingStateEvents.Count == 0)
                    return;

                isPublishingStateEvents = true;
            }

            while (true)
            {
                Action stateEvent;

                lock (stateGate)
                {
                    if (pendingStateEvents.Count == 0)
                    {
                        isPublishingStateEvents = false;
                        break;
                    }

                    stateEvent = pendingStateEvents.Dequeue();
                }

                try
                {
                    stateEvent.Invoke();
                }
                catch (Exception exception)
                {
                    Logs.Error($"Registered room [{RoomId}] state event subscriber failed");
                    Logs.Error(exception);
                }
            }
        }
    }
}
