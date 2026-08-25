using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementsModule : BaseServerModule
    {
        private sealed class AchievementPropertySubscription
        {
            public ObservableServerProfile Profile { get; set; }
            public ObservableAchievements Property { get; set; }
            public ObservableBaseList<AchievementProgressInfo>.ObservableListSetEventDelegate Handler { get; set; }
            public Action<ObservableServerProfile> DisposedHandler { get; set; }
            public HashSet<string> PendingRewardKeys { get; } =
                new HashSet<string>(StringComparer.Ordinal);
        }

        #region INSPECTOR

        [Header("Permission"), SerializeField, Tooltip("Allows an authenticated client to submit progress for its own profile. Room/server updates use the trusted server API and are not controlled by this setting.")]
        protected bool clientCanUpdateProgress = false;

        [Header("Settings"), SerializeField, Tooltip("Catalog of achievement definitions used to create profile entries, validate progress keys and execute configured result commands. Assign the same catalog expected by the game UI.")]
        protected AchievementsDatabase achievementsDatabase;

        #endregion

        protected AuthModule authModule;
        protected ProfilesModule profilesModule;
        protected RoomsModule roomsModule;

        private readonly object subscriptionsSync = new object();
        private readonly Dictionary<string, AchievementPropertySubscription> achievementSubscriptions =
            new Dictionary<string, AchievementPropertySubscription>(StringComparer.Ordinal);
        private readonly List<AchievementProgressInfo> achievementTemplates =
            new List<AchievementProgressInfo>();
        private readonly Dictionary<string, AchievementData> achievementDataByKey =
            new Dictionary<string, AchievementData>(StringComparer.Ordinal);

        protected override void Awake()
        {
            base.Awake();

            AddDependency<AuthModule>();
            AddDependency<ProfilesModule>();
            AddOptionalDependency<RoomsModule>();
        }

        protected virtual void OnDestroy()
        {
            DetachProfileModuleEvents();
            ClearAchievementSubscriptions();
            achievementTemplates.Clear();
            achievementDataByKey.Clear();
        }

        public override void Initialize(IServer server)
        {
            DetachProfileModuleEvents();
            ClearAchievementSubscriptions();

            // Modules dependency setup
            authModule = server.GetModule<AuthModule>();
            profilesModule = server.GetModule<ProfilesModule>();
            roomsModule = server.GetModule<RoomsModule>();
            CacheAchievementDefinitions();

            if (authModule == null)
                logger.Error($"{GetType().Name} should use {nameof(AuthModule)}, but {nameof(AuthModule)} was not found");

            if (profilesModule == null)
                logger.Error($"{GetType().Name} should use {nameof(ProfilesModule)}, but {nameof(ProfilesModule)} was not found");

            if (profilesModule != null)
            {
                profilesModule.OnProfileLoaded += ProfilesModule_OnProfileLoaded;
                profilesModule.OnProfileUnloaded += ProfilesModule_OnProfileUnloaded;
            }

            server.RegisterMessageHandler(MstOpCodes.ServerUpdateAchievementProgress, ServerUpdateAchievementProgressRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientUpdateAchievementProgress, ClientUpdateAchievementProgressRequestHandler);
        }

        protected virtual void ProfilesModule_OnProfileLoaded(ObservableServerProfile profile)
        {
            if (profile.TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements propery))
            {
                foreach (AchievementProgressInfo achievement in achievementTemplates)
                {
                    if (!propery.Has(achievement.key))
                        propery.Add(new AchievementProgressInfo(achievement));
                }

                SubscribeToAchievementChanges(profile, propery);
                SchedulePendingRewards(profile, propery);
            }
            else
            {
                logger.Error("You're using the achievements module, but it looks like you haven't added the achievements property to the profile module's populators database.");
            }
        }

        protected virtual void ProfilesModule_OnProfileUnloaded(ObservableServerProfile profile)
        {
            UnsubscribeFromAchievementChanges(profile);
        }

        private void SubscribeToAchievementChanges(ObservableServerProfile profile,
            ObservableAchievements property)
        {
            var subscription = new AchievementPropertySubscription
            {
                Profile = profile,
                Property = property
            };

            subscription.Handler = (oldItem, newItem) =>
                OnAchievementProgressChanged(profile, property, oldItem, newItem);
            subscription.DisposedHandler = UnsubscribeFromAchievementChanges;

            lock (subscriptionsSync)
            {
                if (achievementSubscriptions.TryGetValue(profile.UserId,
                        out AchievementPropertySubscription previous))
                {
                    DetachAchievementSubscription(previous);
                }

                property.OnSetEvent += subscription.Handler;
                profile.OnDisposedEvent += subscription.DisposedHandler;
                achievementSubscriptions[profile.UserId] = subscription;
            }
        }

        private void UnsubscribeFromAchievementChanges(ObservableServerProfile profile)
        {
            lock (subscriptionsSync)
            {
                if (!achievementSubscriptions.TryGetValue(profile.UserId,
                        out AchievementPropertySubscription subscription) ||
                    !ReferenceEquals(subscription.Profile, profile))
                {
                    return;
                }

                DetachAchievementSubscription(subscription);
                achievementSubscriptions.Remove(profile.UserId);
            }
        }

        private void ClearAchievementSubscriptions()
        {
            lock (subscriptionsSync)
            {
                foreach (AchievementPropertySubscription subscription in achievementSubscriptions.Values)
                    DetachAchievementSubscription(subscription);

                achievementSubscriptions.Clear();
            }
        }

        private static void DetachAchievementSubscription(
            AchievementPropertySubscription subscription)
        {
            subscription.Property.OnSetEvent -= subscription.Handler;
            subscription.Profile.OnDisposedEvent -= subscription.DisposedHandler;
        }

        private void DetachProfileModuleEvents()
        {
            if (profilesModule == null)
                return;

            profilesModule.OnProfileLoaded -= ProfilesModule_OnProfileLoaded;
            profilesModule.OnProfileUnloaded -= ProfilesModule_OnProfileUnloaded;
        }

        private void OnAchievementProgressChanged(ObservableServerProfile profile,
            ObservableAchievements property, AchievementProgressInfo oldItem,
            AchievementProgressInfo newItem)
        {
            if (newItem == null ||
                string.IsNullOrWhiteSpace(newItem.key))
            {
                return;
            }

            if (oldItem != null &&
                string.Equals(oldItem.key, newItem.key, StringComparison.Ordinal) &&
                oldItem.IsUnlocked &&
                (!newItem.IsUnlocked ||
                    oldItem.IsRewardApplied && !newItem.IsRewardApplied))
            {
                lock (profile)
                {
                    property.RestoreUnlockedState(oldItem);
                }

                return;
            }

            if (!newItem.IsUnlocked || newItem.IsRewardApplied)
                return;

            QueuePendingReward(profile, newItem.key);
        }

        private void SchedulePendingRewards(ObservableServerProfile profile,
            ObservableAchievements property)
        {
            var pendingRewardKeys = new List<string>();

            lock (profile)
            {
                foreach (AchievementProgressInfo achievement in property.Value)
                {
                    if (achievement != null && achievement.IsUnlocked && !achievement.IsRewardApplied)
                        pendingRewardKeys.Add(achievement.key);
                }
            }

            foreach (string achievementKey in pendingRewardKeys)
                QueuePendingReward(profile, achievementKey);
        }

        private void QueuePendingReward(ObservableServerProfile profile, string achievementKey)
        {
            if (string.IsNullOrWhiteSpace(achievementKey))
                return;

            lock (subscriptionsSync)
            {
                if (!achievementSubscriptions.TryGetValue(profile.UserId,
                        out AchievementPropertySubscription subscription) ||
                    !ReferenceEquals(subscription.Profile, profile) ||
                    !subscription.PendingRewardKeys.Add(achievementKey))
                {
                    return;
                }
            }

            Mst.Thread.RunInMainThread(() =>
            {
                if (this == null)
                    return;

                try
                {
                    if (TryGetCurrentAchievementSubscription(profile,
                            out AchievementPropertySubscription subscription))
                    {
                        CompleteAchievementUnlock(profile, subscription.Property, achievementKey);
                    }
                }
                finally
                {
                    CompletePendingReward(profile, achievementKey);
                }
            });
        }

        private void CompleteAchievementUnlock(ObservableServerProfile profile,
            ObservableAchievements property, string achievementKey)
        {
            if (!TryGetAchievementData(achievementKey, out AchievementData achievement))
            {
                logger.Warn($"Achievement data not found for key {achievementKey}");
                return;
            }

            lock (profile)
            {
                AchievementProgressInfo progress = property.Get(achievementKey);

                if (progress == null || !progress.IsUnlocked || progress.IsRewardApplied)
                    return;

                try
                {
                    OnAchievementResultCommand(profile, achievement);
                }
                catch (Exception exception)
                {
                    logger.Error($"Achievement {achievementKey} completion failed for user " +
                        $"{profile.UserId}: {exception}");
                    return;
                }

                if (!property.MarkRewardApplied(achievementKey))
                {
                    logger.Warn($"Achievement reward marker was not applied for user " +
                        $"{profile.UserId}, key {achievementKey}");
                    return;
                }
            }

            try
            {
                TryNotifyLoggedInUser(profile, achievementKey);
            }
            catch (Exception exception)
            {
                logger.Error($"Achievement unlock notification failed for user " +
                    $"{profile.UserId}, key {achievementKey}: {exception}");
            }
        }

        private bool TryGetCurrentAchievementSubscription(ObservableServerProfile profile,
            out AchievementPropertySubscription subscription)
        {
            lock (subscriptionsSync)
            {
                return achievementSubscriptions.TryGetValue(profile.UserId, out subscription) &&
                       ReferenceEquals(subscription.Profile, profile);
            }
        }

        private void CompletePendingReward(ObservableServerProfile profile, string achievementKey)
        {
            lock (subscriptionsSync)
            {
                if (achievementSubscriptions.TryGetValue(profile.UserId,
                        out AchievementPropertySubscription subscription) &&
                    ReferenceEquals(subscription.Profile, profile))
                {
                    subscription.PendingRewardKeys.Remove(achievementKey);
                }
            }
        }

        private void TryNotifyLoggedInUser(ObservableServerProfile profile, string achievementKey)
        {
            if (authModule == null ||
                !authModule.TryGetLoggedInUserById(profile.UserId, out IUserPeerExtension user) ||
                user?.Peer == null ||
                !user.Peer.IsConnected ||
                !user.Peer.TryGetExtension(out ProfilePeerExtension profileExtension) ||
                !ReferenceEquals(profileExtension.Profile, profile))
            {
                return;
            }

            user.Peer.SendMessage(MstOpCodes.ClientAchievementUnlocked, achievementKey);
        }

        private bool TryGetAchievementData(string key, out AchievementData achievement)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                achievement = null;
                return false;
            }

            return achievementDataByKey.TryGetValue(key, out achievement) && achievement != null;
        }

        protected void CacheAchievementDefinitions()
        {
            achievementTemplates.Clear();
            achievementDataByKey.Clear();

            if (achievementsDatabase == null)
            {
                logger.Error($"{nameof(AchievementsDatabase)} is not assigned");
                return;
            }

            foreach (AchievementData achievement in achievementsDatabase)
            {
                if (achievement == null || string.IsNullOrWhiteSpace(achievement.key))
                    continue;

                if (achievementDataByKey.ContainsKey(achievement.key))
                {
                    logger.Warn($"Duplicate achievement key ignored: {achievement.key}");
                    continue;
                }

                achievementDataByKey.Add(achievement.key, achievement);
                achievementTemplates.Add(new AchievementProgressInfo(achievement));
            }
        }

        /// <summary>
        /// Executes project-specific result commands after an achievement becomes unlocked.
        /// </summary>
        /// <param name="profile">Authoritative profile receiving the result.</param>
        /// <param name="achievement">Achievement that transitioned to the unlocked state.</param>
        protected virtual void OnAchievementResultCommand(ObservableServerProfile profile,
            AchievementData achievement) { }

        #region MESSAGES

        protected virtual async Task ClientUpdateAchievementProgressRequestHandler(
            IIncomingMessage message, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!clientCanUpdateProgress)
                {
                    RespondErrorIfExpected(message, ResponseStatus.Forbidden,
                        MstErrorCodes.ACHIEVEMENT_UPDATE_FORBIDDEN);
                    return;
                }

                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                if (userExtension == null)
                {
                    RespondErrorIfExpected(message, ResponseStatus.Unauthorized,
                        MstErrorCodes.USER_IS_NOT_LOGGED_IN);
                    return;
                }

                var data = message.AsPacket<UpdateAchievementProgressPacket>();

                // if a user has joined a room, do not update their profile directly
                // we need to update his profile in the room, which itself transmits all the data to the player.
                if (userExtension.HasJoinedRoom())
                {
                    if (roomsModule == null)
                    {
                        RespondErrorIfExpected(message, ResponseStatus.ServiceUnavailable,
                            MstErrorCodes.ROOMS_MODULE_UNAVAILABLE);
                        return;
                    }

                    if (!roomsModule.TryGetRoomById(userExtension.JoinedRoomID, out var room))
                    {
                        RespondErrorIfExpected(message, ResponseStatus.DependencyError,
                            MstErrorCodes.JOINED_ROOM_NOT_FOUND);
                        return;
                    }

                    data.userId = userExtension.UserId;
                    ForwardClientProgressToRoom(message, room, data);
                    return;
                }

                if (!userExtension.Peer.TryGetExtension(out ProfilePeerExtension profile))
                {
                    RespondErrorIfExpected(message, ResponseStatus.NotFound,
                        MstErrorCodes.USER_PROFILE_NOT_FOUND);
                    return;
                }

                if (!profile.Profile.TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements property))
                {
                    RespondErrorIfExpected(message, ResponseStatus.NotFound,
                        MstErrorCodes.USER_ACHIEVEMENTS_NOT_FOUND);
                    return;
                }

                if (!property.Has(data.key))
                {
                    var properties = new MstProperties();
                    properties.Set(MstErrorPropertyKeys.ACHIEVEMENT_KEY, data.key);
                    RespondErrorIfExpected(message, ResponseStatus.NotFound,
                        MstErrorCodes.ACHIEVEMENT_NOT_FOUND, properties);
                    return;
                }

                bool unlockedNow = false;

                await RunInMainThreadAsync(() =>
                {
                    lock (profile.Profile)
                    {
                        unlockedNow = property.TryToUnlock(data.key, data.progress);
                    }
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                RespondIfExpected(message, unlockedNow);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.Error($"Client achievement progress update failed: {ex}");
                RespondErrorIfExpected(message, ResponseStatus.Error,
                    MstErrorCodes.ACHIEVEMENT_UPDATE_FAILED);
            }
        }

        private void ForwardClientProgressToRoom(IIncomingMessage clientMessage, RegisteredRoom room,
            UpdateAchievementProgressPacket data)
        {
            if (!clientMessage.IsExpectingResponse)
            {
                room.Peer.SendMessage(MstOpCodes.ServerUpdateAchievementProgress, data);
                return;
            }

            try
            {
                room.Peer.SendMessage(MstOpCodes.ServerUpdateAchievementProgress, data, (status, response) =>
                {
                    if (!clientMessage.Peer.IsConnected)
                        return;

                    if (status != ResponseStatus.Success)
                    {
                        if (response != null && response.HasData)
                        {
                            try
                            {
                                clientMessage.Respond(response.AsBytes(), status);
                                return;
                            }
                            catch (Exception exception)
                            {
                                logger.Warn($"Failed to forward structured achievement error from room " +
                                    $"{room.RoomId}: {exception}");
                            }
                        }

                        clientMessage.RespondError(status,
                            MstErrorCodes.ROOM_ACHIEVEMENT_FORWARD_FAILED);
                        return;
                    }

                    if (response == null || !response.HasData)
                    {
                        clientMessage.RespondError(ResponseStatus.DependencyError,
                            MstErrorCodes.ROOM_ACHIEVEMENT_RESPONSE_MISSING);
                        return;
                    }

                    try
                    {
                        clientMessage.Respond(response.AsBool(), ResponseStatus.Success);
                    }
                    catch (Exception exception)
                    {
                        logger.Error($"Invalid achievement response received from room {room.RoomId}: {exception}");
                        clientMessage.RespondError(ResponseStatus.DependencyError,
                            MstErrorCodes.ROOM_ACHIEVEMENT_RESPONSE_INVALID);
                    }
                });
            }
            catch (Exception exception)
            {
                logger.Error($"Failed to forward achievement progress to room {room.RoomId}: {exception}");
                clientMessage.RespondError(ResponseStatus.DependencyError,
                    MstErrorCodes.ROOM_ACHIEVEMENT_FORWARD_FAILED);
            }
        }

        protected virtual bool HasPermissionToUpdateAchievements(IPeer peer)
        {
            var security = peer.GetExtension<SecurityInfoPeerExtension>();
            return security != null && security.HasPermission(MstPermissionKeys.RoomServer);
        }

        protected virtual async Task ServerUpdateAchievementProgressRequestHandler(
            IIncomingMessage message, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!HasPermissionToUpdateAchievements(message.Peer))
                {
                    logger.Warn($"Achievement server update denied for peer {message.Peer.Id}. " +
                        $"Required permission: {MstPermissionKeys.RoomServer}");
                    RespondErrorIfExpected(message, ResponseStatus.Forbidden,
                        MstErrorCodes.PERMISSION_DENIED);
                    return;
                }

                var updateList = message.AsPacketsList<UpdateAchievementProgressPacket>();
                int skippedUpdates = 0;

                await RunInMainThreadAsync(() =>
                {
                    foreach (UpdateAchievementProgressPacket data in updateList)
                    {
                        if (!TryApplyServerProgress(data))
                        {
                            skippedUpdates++;
                            logger.Warn($"Achievement update skipped because profile or achievement " +
                                $"{data?.key} was not found for user {data?.userId}");
                        }
                    }
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (skippedUpdates > 0)
                {
                    var properties = new MstProperties();
                    properties.Set(MstErrorPropertyKeys.COUNT, skippedUpdates);
                    RespondErrorIfExpected(message, ResponseStatus.NotFound,
                        MstErrorCodes.ACHIEVEMENT_UPDATES_NOT_APPLIED, properties);
                    return;
                }

                RespondIfExpected(message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.Error($"Server achievement progress update failed: {ex}");
                RespondErrorIfExpected(message, ResponseStatus.Error,
                    MstErrorCodes.ACHIEVEMENT_UPDATE_FAILED);
            }
        }

        private bool TryApplyServerProgress(UpdateAchievementProgressPacket data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.userId) ||
                string.IsNullOrWhiteSpace(data.key))
            {
                return false;
            }

            AchievementPropertySubscription subscription;

            lock (subscriptionsSync)
            {
                if (!achievementSubscriptions.TryGetValue(data.userId,
                        out subscription))
                    return false;
            }

            lock (subscription.Profile)
            {
                lock (subscriptionsSync)
                {
                    if (!achievementSubscriptions.TryGetValue(data.userId,
                            out AchievementPropertySubscription current) ||
                        !ReferenceEquals(current, subscription) ||
                        !subscription.Property.Has(data.key))
                    {
                        return false;
                    }
                }

                subscription.Property.TrySetProgress(data.key, data.progress);
                return true;
            }
        }

        private static async Task RunInMainThreadAsync(Action action,
            CancellationToken cancellationToken)
        {
            if (action == null)
                return;

            cancellationToken.ThrowIfCancellationRequested();

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int executionState = 0;

            using (cancellationToken.Register(() =>
            {
                if (Interlocked.CompareExchange(ref executionState, 2, 0) == 0)
                    completion.TrySetCanceled();
            }))
            {
                Mst.Thread.RunInMainThread(() =>
                {
                    if (Interlocked.CompareExchange(ref executionState, 1, 0) != 0)
                        return;

                    try
                    {
                        action.Invoke();
                        Interlocked.Exchange(ref executionState, 2);
                        completion.TrySetResult(true);
                    }
                    catch (Exception exception)
                    {
                        Interlocked.Exchange(ref executionState, 2);
                        completion.TrySetException(exception);
                    }
                });

                await completion.Task;
            }
        }

        private static void RespondIfExpected(IIncomingMessage message)
        {
            if (message.IsExpectingResponse)
                message.Respond(ResponseStatus.Success);
        }

        private static void RespondIfExpected(IIncomingMessage message, bool value)
        {
            if (message.IsExpectingResponse)
                message.Respond(value, ResponseStatus.Success);
        }

        private static void RespondErrorIfExpected(
            IIncomingMessage message,
            ResponseStatus status,
            string code,
            MstProperties properties = null)
        {
            if (message.IsExpectingResponse)
                message.RespondError(status, code, properties);
        }

        #endregion
    }
}
