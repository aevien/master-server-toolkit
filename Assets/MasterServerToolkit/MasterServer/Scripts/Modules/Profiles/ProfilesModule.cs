using MasterServerToolkit.DebounceThrottle;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Handles player profiles within master server.
    /// Listens to changes in player profiles, and sends updates to
    /// clients of interest.
    /// Also, reads changes from game server, and applies them to players profile
    /// </summary>
    public class ProfilesModule : BaseServerModule
    {
        private sealed class ProfileLoadOperation
        {
            private int ownerClaimed;

            public TaskCompletionSource<ObservableServerProfile> Completion { get; } =
                new TaskCompletionSource<ObservableServerProfile>(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool TryClaimOwnership()
            {
                return Interlocked.CompareExchange(ref ownerClaimed, 1, 0) == 0;
            }
        }

        private sealed class PendingProfileSave
        {
            public PendingProfileSave(ObservableServerProfile profile)
            {
                Profile = profile;
            }

            public ObservableServerProfile Profile { get; }
        }

        #region INSPECTOR

        /// <summary>
        /// Time to pass after logging out, until profile
        /// will be removed from the lookup. Should be enough for game
        /// server to submit last changes
        /// </summary>
        [Header("General Settings")]
        [SerializeField, Tooltip("Delay in seconds after logout before the profile is removed from memory. The delay lets an active room submit its final changes. 0 starts unload immediately after logout.")]
        protected int unloadProfileAfter = 20;

        /// <summary>
        /// Interval, in which updated profiles will be saved to database
        /// </summary>
        [SerializeField, Tooltip("Throttle interval in seconds for normal dirty-profile database saves. Repeated changes inside the interval are batched. 0 schedules batches without an intentional delay.")]
        protected int saveProfileDebounceTime = 1;

        /// <summary>
        /// Interval, in which profile updates will be sent to clients
        /// </summary>
        [SerializeField, Tooltip("Throttle interval in seconds for sending accumulated profile changes to interested clients. 0 schedules updates without an intentional delay.")]
        protected int clientUpdateDebounceTime = 1;

        /// <summary>
        /// Max update size in bytes to prevent memory attack
        /// </summary>
        [SerializeField, Tooltip("Maximum accepted serialized profile-update payload in bytes. Larger client or room updates are rejected before deserialization. Use a positive value large enough for the complete permitted delta.")]
        protected int maxUpdateSize = 1048576;

        [Header("Timeout Settings")]
        [SerializeField, Tooltip("Maximum time in seconds allowed for database restore and profile preparation. A timeout fails that load attempt; use a positive value.")]
        protected int profileLoadTimeoutSeconds = 10;

        /// <summary>
        /// Database accessor factory that helps to create integration with profile db
        /// </summary>
        [Tooltip("Factory that creates the authoritative profile database accessor. It is required for restoring and persisting profiles.")]
        public DatabaseAccessorFactory databaseAccessorFactory;

        [SerializeField, Tooltip("Database of property populators that defines the profile schema, default values and synchronization behavior. Assign the same schema expected by clients and room servers.")]
        private ObservablePropertyPopulatorsDatabase populatorsDatabase;

        #endregion

        protected ThrottleDispatcher saveDebounceDispatcher;
        protected ThrottleDispatcher sendDebounceDispatcher;

        /// <summary>
        /// Auth module for listening to auth events
        /// </summary>
        protected AuthModule authModule;

        /// <summary>
        /// DB to work with profile data
        /// </summary>
        protected IProfilesDatabaseAccessor databaseAccessor;

        /// <summary>
        /// Database accessor for profiles
        /// </summary>
        public IProfilesDatabaseAccessor DatabaseAccessor
        {
            get => databaseAccessor;
            set => databaseAccessor = value;
        }

        /// <summary>
        /// List of the users profiles
        /// </summary>
        protected readonly ConcurrentDictionary<string, ObservableServerProfile> profilesList = new ConcurrentDictionary<string, ObservableServerProfile>();

        /// <summary>
        /// 
        /// </summary>
        private readonly ConcurrentDictionary<string, PendingProfileSave> profilesListToSave =
            new ConcurrentDictionary<string, PendingProfileSave>();

        private readonly ConcurrentDictionary<string, ObservableServerProfile> profilesPendingUnload =
            new ConcurrentDictionary<string, ObservableServerProfile>();

        /// <summary>
        /// List of all profile updates to be sent to clients
        /// </summary>
        protected readonly ConcurrentDictionary<string, ObservableServerProfile> profilesListToSend = new ConcurrentDictionary<string, ObservableServerProfile>();

        /// <summary>
        /// Profiles that are running initial load hooks and are not visible to clients yet.
        /// </summary>
        protected readonly ConcurrentDictionary<string, byte> profilesInitialLoadInProgress = new ConcurrentDictionary<string, byte>();
        private readonly ConcurrentDictionary<string, ProfileLoadOperation> profileLoadOperations =
            new ConcurrentDictionary<string, ProfileLoadOperation>();
        private readonly CancellationTokenSource profileLifecycleCancellation = new CancellationTokenSource();
        private readonly SemaphoreSlim profilePersistenceSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim[] profileOperationSemaphores = Enumerable.Range(0, 64)
            .Select(_ => new SemaphoreSlim(1, 1))
            .ToArray();
        private readonly object persistenceLifecycleSync = new object();
        private bool acceptsPersistenceOperations = true;
        private long persistenceRunGeneration;

        /// <summary>
        /// Gets list of userprofiles
        /// </summary>
        public IEnumerable<ObservableServerProfile> Profiles => profilesList.Values;

        /// <summary>
        /// Number of profiles waiting for a confirmed database save.
        /// </summary>
        protected int PendingProfileSaveCount => profilesListToSave.Count;

        /// <summary>
        /// It is performed after loading the profile from the database
        /// </summary>
        public event OnServerProfileHandler OnProfileLoaded;

        /// <summary>
        /// It is performed after unloading the profile to the database
        /// </summary>
        public event OnServerProfileHandler OnProfileUnloaded;

        protected override void Awake()
        {
            base.Awake();

            // Add auth module as a dependency of this module
            AddOptionalDependency<AuthModule>();

            // Set debounce dispatchers
            saveDebounceDispatcher = new ThrottleDispatcher(saveProfileDebounceTime * 1000);
            sendDebounceDispatcher = new ThrottleDispatcher(clientUpdateDebounceTime * 1000);
        }

        public override void Initialize(IServer server)
        {
            if (databaseAccessorFactory)
                databaseAccessorFactory.CreateAccessors();

            databaseAccessor = Mst.Server.DbAccessors.GetAccessor<IProfilesDatabaseAccessor>();

            if (databaseAccessor == null)
            {
                logger.Fatal($"Profiles database implementation was not found in {GetType().Name}");
                return;
            }

            // Auth dependency setup
            authModule = server.GetModule<AuthModule>();

            if (authModule)
            {
                authModule.OnUserLoggedInEvent += OnUserLoggedInEventHandler;
                authModule.OnUserLoggedOutEvent += OnUserLoggedOutEvent;
            }
            else
            {
                logger.Error($"{GetType().Name} cannot be used without {nameof(AuthModule)}, use {nameof(AuthModule)} module to be able to use this one");
            }

            server.RegisterMessageHandler(MstOpCodes.ServerFillInProfileValues, ServerFillInProfileValuesRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerUpdateProfileValues, ServerUpdateProfileValuesHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientFillInProfileValues, ClientFillInProfileValuesRequestHandler);
        }

        public override void StartServerRun(CancellationToken runCancellationToken)
        {
            lock (persistenceLifecycleSync)
            {
                persistenceRunGeneration++;
                acceptsPersistenceOperations = true;
            }

            CancelInvoke(nameof(UpdateThrottle));
            InvokeRepeating(nameof(UpdateThrottle), 0.1f, 0.1f);
        }

        public override async Task StopServerRunAsync()
        {
            ClosePersistenceOperations();

            if (this != null)
                CancelInvoke(nameof(UpdateThrottle));

            await SavePendingProfilesAsync().ConfigureAwait(false);

            if (profilesListToSave.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Profile shutdown flush left {profilesListToSave.Count} profile(s) pending");
            }
        }

        protected virtual void OnDestroy()
        {
            ClosePersistenceOperations();

            CancelInvoke(nameof(UpdateThrottle));
            profileLifecycleCancellation.Cancel();

            foreach (ProfileLoadOperation operation in profileLoadOperations.Values)
                operation.Completion.TrySetResult(null);

            profileLoadOperations.Clear();
            profilesInitialLoadInProgress.Clear();
            profilesPendingUnload.Clear();
            profileLifecycleCancellation.Dispose();

            if (authModule)
            {
                authModule.OnUserLoggedInEvent -= OnUserLoggedInEventHandler;
                authModule.OnUserLoggedOutEvent -= OnUserLoggedOutEvent;
            }
        }

        protected virtual void UpdateThrottle()
        {
            if (profilesListToSave.Count > 0)
                saveDebounceDispatcher.ThrottleAsync(SavePendingProfilesAsync);

            if (profilesListToSend.Count <= 0)
                return;

            sendDebounceDispatcher.Throttle(() =>
            {
                ObservableServerProfile[] snapshot = profilesListToSend.Values.ToArray();
                profilesListToSend.Clear();

                try
                {
                    foreach (ObservableServerProfile profile in snapshot)
                    {
                        lock (profile)
                        {
                            byte[] updates = profile.GetUpdates();
                            IPeer clientPeer = profile.ClientPeer;

                            if (clientPeer != null && clientPeer.IsConnected)
                            {
                                logger.Info($"Sending profile update to client. userId={profile.UserId}, updateBytes={updates.Length}",
                                    LogChannels.Economy);
                                clientPeer.SendMessage(MessageHelper.Create(
                                    MstOpCodes.UpdateClientProfile, updates));
                            }
                            else
                            {
                                logger.Warn($"Profile update was not sent to client. reason=client_not_connected, userId={profile.UserId}",
                                    LogChannels.Economy);
                            }

                            profile.ClearUpdates();
                        }
                    }
                }
                catch (Exception ex)
                {
                    foreach (ObservableServerProfile profile in snapshot)
                    {
                        if (profile.HasDirtyProperties)
                        {
                            profilesListToSend.AddOrUpdate(
                                profile.UserId, profile, (key, oldValue) => profile);
                        }
                    }

                    logger.Error($"Error sending profile updates: {ex}");
                }
            });
        }

        /// <summary>
        /// Triggered when the user has successfully logged in
        /// </summary>
        /// <param name="user">Logged-in user.</param>
        /// <param name="cancellationToken">Cancellation token for the current server run.</param>
        protected virtual Task OnUserLoggedInEventHandler(IUserPeerExtension user,
            CancellationToken cancellationToken)
        {
            return HandleUserLoggedInAsync(user, cancellationToken);
        }

        /// <summary>
        /// Starts profile restoration using the module lifecycle token.
        /// </summary>
        /// <param name="user">Logged-in user.</param>
        /// <returns>Profile restoration task.</returns>
        protected virtual Task OnUserLoggedInEventHandler(IUserPeerExtension user)
        {
            return HandleUserLoggedInAsync(user);
        }

        protected virtual async Task HandleUserLoggedInAsync(IUserPeerExtension user)
        {
            await HandleUserLoggedInAsync(user, profileLifecycleCancellation.Token);
        }

        protected virtual async Task HandleUserLoggedInAsync(IUserPeerExtension user,
            CancellationToken serverCancellationToken)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                profileLifecycleCancellation.Token,
                serverCancellationToken);
            CancellationToken cancellationToken = linkedCancellation.Token;

            cancellationToken.ThrowIfCancellationRequested();

            if (user == null || string.IsNullOrEmpty(user.UserId))
                return;

            if (profilesList.TryGetValue(user.UserId, out ObservableServerProfile readyProfile))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryAttachProfileToCurrentSession(user, readyProfile))
                    return;
            }

            ProfileLoadOperation operation = profileLoadOperations.GetOrAdd(
                user.UserId,
                _ => new ProfileLoadOperation());

            if (operation.TryClaimOwnership())
                await RunInitialProfileLoadAsync(user.UserId, operation, cancellationToken);

            ObservableServerProfile profile = await operation.Completion.Task;
            cancellationToken.ThrowIfCancellationRequested();

            if (profile != null)
                TryAttachProfileToCurrentSession(user, profile);
        }

        private async Task RunInitialProfileLoadAsync(string userId, ProfileLoadOperation operation,
            CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            int retryCount = 0;

            profilesInitialLoadInProgress.TryAdd(userId, 0);

            try
            {
                if (profilesList.TryGetValue(userId, out ObservableServerProfile existingProfile))
                {
                    operation.Completion.TrySetResult(existingProfile);
                    return;
                }

                while (retryCount < maxRetries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!TryGetCurrentLoggedInUser(userId, out IUserPeerExtension currentUser))
                    {
                        operation.Completion.TrySetResult(null);
                        return;
                    }

                    logger.Debug($"User {userId} needs to create or load profile from db");

                    ObservableServerProfile candidate = null;

                    try
                    {
                        candidate = CreateProfile(userId, currentUser.Peer);
                        candidate.UnloadDebounceDispatcher = new DebounceDispatcher(unloadProfileAfter * 1000);

                        SemaphoreSlim operationSemaphore = GetProfileOperationSemaphore(userId);
                        await operationSemaphore.WaitAsync(cancellationToken);

                        try
                        {
                            await databaseAccessor.RestoreProfileAsync(candidate, cancellationToken);
                        }
                        finally
                        {
                            operationSemaphore.Release();
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        if (!TryGetCurrentLoggedInUser(userId, out currentUser))
                        {
                            CleanupUnpublishedProfile(candidate);
                            operation.Completion.TrySetResult(null);
                            return;
                        }

                        candidate.ClientPeer = currentUser.Peer;

                        await PrepareProfileAsync(candidate);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!TryGetCurrentLoggedInUser(userId, out currentUser))
                        {
                            CleanupUnpublishedProfile(candidate);
                            operation.Completion.TrySetResult(null);
                            return;
                        }

                        candidate.ClientPeer = currentUser.Peer;
                        cancellationToken.ThrowIfCancellationRequested();
                        NotifyProfileLoaded(candidate);

                        if (!TryGetCurrentLoggedInUser(userId, out currentUser))
                        {
                            CleanupUnpublishedProfile(candidate);
                            operation.Completion.TrySetResult(null);
                            return;
                        }

                        candidate.ClientPeer = currentUser.Peer;
                        bool hasInitialChanges = candidate.HasDirtyProperties;
                        candidate.ClearUpdates();
                        TryRemoveExact(profilesListToSend, userId, candidate);
                        ObservableServerProfile publishedProfile = candidate;

                        lock (candidate)
                        {
                            if (!profilesList.TryAdd(userId, candidate))
                            {
                                if (!profilesList.TryGetValue(userId, out existingProfile))
                                    throw new InvalidOperationException($"Profile {userId} could not be published");

                                CleanupUnpublishedProfile(candidate);
                                publishedProfile = existingProfile;
                            }
                            else
                            {
                                candidate.OnModifiedInServerEvent += OnProfileChangedEventHandler;

                                if (hasInitialChanges)
                                    QueueProfileToSave(candidate);
                            }
                        }

                        candidate = publishedProfile;

                        profilesInitialLoadInProgress.TryRemove(userId, out _);
                        operation.Completion.TrySetResult(candidate);
                        return;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        CleanupUnpublishedProfile(candidate);
                        operation.Completion.TrySetResult(null);
                        return;
                    }
                    catch (Exception ex)
                    {
                        CleanupUnpublishedProfile(candidate);
                        retryCount++;

                        logger.Error($"Attempt {retryCount}/{maxRetries} failed: {ex.Message}");

                        if (retryCount >= maxRetries)
                        {
                            logger.Error($"Failed to load profile for user {userId} after {maxRetries} attempts: {ex}");
                            operation.Completion.TrySetResult(null);
                            return;
                        }

                        await DelayProfileLoadRetryAsync(retryCount, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                operation.Completion.TrySetResult(null);
            }
            catch (Exception ex)
            {
                logger.Error($"Unexpected initial profile load failure for user {userId}: {ex}");
                operation.Completion.TrySetResult(null);
            }
            finally
            {
                profilesInitialLoadInProgress.TryRemove(userId, out _);
                TryRemoveExact(profileLoadOperations, userId, operation);
            }
        }

        protected virtual Task DelayProfileLoadRetryAsync(int retryCount, CancellationToken cancellationToken)
        {
            return Task.Delay(500 * retryCount, cancellationToken);
        }

        protected virtual Task PrepareProfileAsync(ObservableServerProfile profile)
        {
            return Task.CompletedTask;
        }

        protected virtual async Task SavePendingProfilesAsync()
        {
            KeyValuePair<string, PendingProfileSave>[] snapshot = null;
            List<ObservableServerProfile> profiles = null;
            List<KeyValuePair<string, PendingProfileSave>> persistedEntries = null;

            await profilePersistenceSemaphore.WaitAsync();

            try
            {
                snapshot = profilesListToSave.ToArray();

                if (snapshot.Length == 0)
                    return;

                profiles = snapshot
                    .Select(item => item.Value.Profile)
                    .ToList();

                logger.Info($"Saving profile snapshot. profiles={profiles.Count}, users={string.Join(",", profiles.Select(p => p.UserId))}",
                    LogChannels.Economy);

                await databaseAccessor.UpdateProfilesAsync(profiles);
                persistedEntries = new List<KeyValuePair<string, PendingProfileSave>>(snapshot.Length);

                foreach (KeyValuePair<string, PendingProfileSave> item in snapshot)
                {
                    if (TryRemoveExact(profilesListToSave, item.Key, item.Value))
                        persistedEntries.Add(item);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error saving profiles. profiles={profiles?.Count ?? 0}, pending={profilesListToSave.Count}: {ex}");
                return;
            }
            finally
            {
                profilePersistenceSemaphore.Release();
            }

            foreach (KeyValuePair<string, PendingProfileSave> item in persistedEntries)
            {
                try
                {
                    TryFinalizeProfileUnload(item.Value.Profile);
                }
                catch (Exception ex)
                {
                    logger.Error($"Profile unload finalization failed after a successful save. userId={item.Key}: {ex}");
                }
            }

            logger.Info($"Profile snapshot saved. profiles={profiles.Count}, pending={profilesListToSave.Count}",
                LogChannels.Economy);
        }

        /// <summary>
        /// Saves a profile after all previously started operations for that user and
        /// profile persistence batches have completed.
        /// </summary>
        /// <param name="profile">Profile whose current state must be durable before this call completes.</param>
        /// <param name="cancellationToken">Token used while waiting to enter the persistence queue.</param>
        public async Task SaveProfileAsync(ObservableServerProfile profile,
            CancellationToken cancellationToken = default)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (databaseAccessor == null)
                throw new InvalidOperationException("Profiles database accessor is not initialized");

            EnsurePersistenceOperationsAllowed();

            SemaphoreSlim operationSemaphore = GetProfileOperationSemaphore(
                profile.UserId);
            await operationSemaphore.WaitAsync(cancellationToken);

            try
            {
                await SaveProfileCoreAsync(profile, cancellationToken);
            }
            finally
            {
                operationSemaphore.Release();
            }
        }

        private async Task SaveProfileCoreAsync(ObservableServerProfile profile,
            CancellationToken cancellationToken)
        {
            await profilePersistenceSemaphore.WaitAsync(cancellationToken);

            PendingProfileSave pendingSave = null;
            bool pendingSaveRemoved = false;

            try
            {
                EnsurePersistenceOperationsAllowed();

                Task persistenceTask;

                lock (profile)
                {
                    if (profilesListToSave.TryGetValue(profile.UserId,
                        out PendingProfileSave currentPendingSave) &&
                        ReferenceEquals(currentPendingSave.Profile, profile))
                    {
                        pendingSave = currentPendingSave;
                    }

                    persistenceTask = databaseAccessor.UpdateProfileAsync(
                        profile, CancellationToken.None);
                }

                await persistenceTask;

                if (pendingSave != null)
                {
                    pendingSaveRemoved = TryRemoveExact(
                        profilesListToSave, profile.UserId, pendingSave);
                }
            }
            finally
            {
                profilePersistenceSemaphore.Release();
            }

            if (pendingSaveRemoved)
                TryFinalizeProfileUnload(profile);
        }

        /// <summary>
        /// Restores, updates, and persists a profile only while its user remains offline.
        /// </summary>
        /// <param name="userId">User whose profile should be updated.</param>
        /// <param name="update">
        /// Update callback. Return <c>false</c> to leave the restored profile unchanged.
        /// </param>
        /// <param name="cancellationToken">Token used while waiting for the operation.</param>
        /// <returns>
        /// The restored profile when the user stayed offline; otherwise <c>null</c>.
        /// The caller owns and must dispose a returned profile.
        /// </returns>
        public async Task<ObservableServerProfile> TryUpdateOfflineProfileAsync(
            string userId, Func<ObservableServerProfile, bool> update,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User id cannot be empty", nameof(userId));

            if (update == null)
                throw new ArgumentNullException(nameof(update));

            if (databaseAccessor == null)
                throw new InvalidOperationException("Profiles database accessor is not initialized");

            EnsurePersistenceOperationsAllowed();

            SemaphoreSlim operationSemaphore = GetProfileOperationSemaphore(userId);
            await operationSemaphore.WaitAsync(cancellationToken);

            try
            {
                await profilePersistenceSemaphore.WaitAsync(cancellationToken);

                try
                {
                    EnsurePersistenceOperationsAllowed();

                    if (TryGetCurrentLoggedInUser(userId, out _) ||
                        profilesList.ContainsKey(userId))
                        return null;

                    ObservableServerProfile profile = CreateProfile(userId);

                    try
                    {
                        await databaseAccessor.RestoreProfileAsync(profile, cancellationToken);

                        if (TryGetCurrentLoggedInUser(userId, out _) ||
                            profilesList.ContainsKey(userId))
                        {
                            profile.Dispose();
                            return null;
                        }

                        Task persistenceTask;

                        lock (profile)
                        {
                            if (!update(profile))
                                return profile;

                            persistenceTask = databaseAccessor.UpdateProfileAsync(
                                profile, CancellationToken.None);
                        }

                        await persistenceTask;
                        return profile;
                    }
                    catch
                    {
                        profile.Dispose();
                        throw;
                    }
                }
                finally
                {
                    profilePersistenceSemaphore.Release();
                }
            }
            finally
            {
                operationSemaphore.Release();
            }
        }

        private SemaphoreSlim GetProfileOperationSemaphore(string userId)
        {
            uint hash = unchecked((uint)StringComparer.Ordinal.GetHashCode(userId));
            int index = (int)(hash % (uint)profileOperationSemaphores.Length);
            return profileOperationSemaphores[index];
        }

        private bool TryGetCurrentLoggedInUser(string userId, out IUserPeerExtension user)
        {
            user = null;
            return authModule != null && authModule.TryGetLoggedInUserById(userId, out user) && user != null;
        }

        private bool TryAttachProfileToCurrentSession(IUserPeerExtension user, ObservableServerProfile profile)
        {
            if (user?.Peer == null || profile == null || !user.Peer.IsConnected)
                return false;

            lock (profile)
            {
                if (!TryGetCurrentLoggedInUser(user.UserId, out IUserPeerExtension currentUser) ||
                    !ReferenceEquals(currentUser, user) ||
                    !profilesList.TryGetValue(user.UserId, out ObservableServerProfile currentProfile) ||
                    !ReferenceEquals(currentProfile, profile))
                {
                    return false;
                }

                profile.UnloadDebounceDispatcher?.Cancel();
                TryRemoveExact(profilesPendingUnload, user.UserId, profile);
                profile.ClientPeer = user.Peer;
                user.Peer.AddExtension(new ProfilePeerExtension(profile, user.Peer));
                return true;
            }
        }

        private void CleanupUnpublishedProfile(ObservableServerProfile profile)
        {
            if (profile == null)
                return;

            profile.OnModifiedInServerEvent -= OnProfileChangedEventHandler;
            TryRemovePendingProfileSave(profile);
            TryRemoveExact(profilesPendingUnload, profile.UserId, profile);
            TryRemoveExact(profilesListToSend, profile.UserId, profile);
            profile.Dispose();
        }

        private void TryRemovePendingProfileSave(ObservableServerProfile profile)
        {
            if (profilesListToSave.TryGetValue(profile.UserId, out PendingProfileSave pendingSave) &&
                ReferenceEquals(pendingSave.Profile, profile))
            {
                TryRemoveExact(profilesListToSave, profile.UserId, pendingSave);
            }
        }

        private void TryFinalizeProfileUnload(ObservableServerProfile profile)
        {
            bool removed = false;

            lock (profile)
            {
                if (authModule != null && authModule.IsUserLoggedInById(profile.UserId))
                {
                    TryRemoveExact(profilesPendingUnload, profile.UserId, profile);
                    return;
                }

                if (!profilesPendingUnload.TryGetValue(profile.UserId, out ObservableServerProfile pendingProfile) ||
                    !ReferenceEquals(pendingProfile, profile) ||
                    !profilesList.TryGetValue(profile.UserId, out ObservableServerProfile currentProfile) ||
                    !ReferenceEquals(currentProfile, profile))
                {
                    return;
                }

                if (profilesListToSave.TryGetValue(profile.UserId, out PendingProfileSave pendingSave) &&
                    ReferenceEquals(pendingSave.Profile, profile))
                {
                    return;
                }

                removed = TryRemoveExact(profilesList, profile.UserId, profile);

                if (removed)
                {
                    TryRemoveExact(profilesPendingUnload, profile.UserId, profile);
                    profile.OnModifiedInServerEvent -= OnProfileChangedEventHandler;
                }
            }

            if (removed)
            {
                OnProfileUnloaded?.Invoke(profile);
                logger.Debug($"Profile for user {profile.UserId} has been unloaded after a successful save");
            }
        }

        private static bool TryRemoveExact<TValue>(ConcurrentDictionary<string, TValue> dictionary,
            string key, TValue value) where TValue : class
        {
            return ((ICollection<KeyValuePair<string, TValue>>)dictionary)
                .Remove(new KeyValuePair<string, TValue>(key, value));
        }

        protected void NotifyProfileLoaded(ObservableServerProfile profile)
        {
            ProfileLoaded(profile);
            OnProfileLoaded?.Invoke(profile);
        }

        protected virtual void ProfileLoaded(ObservableServerProfile profile) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        protected virtual void OnUserLoggedOutEvent(IUserPeerExtension user)
        {
            user.Peer.ClearExtension<ProfilePeerExtension>();

            if (TryGetCurrentLoggedInUser(user.UserId, out IUserPeerExtension currentUser) &&
                !ReferenceEquals(currentUser, user))
            {
                return;
            }

            UnloadProfile(user.UserId);
        }

        /// <summary>
        /// Invoked, when profile is changed
        /// </summary>
        /// <param name="profile"></param>
        protected virtual void OnProfileChangedEventHandler(ObservableServerProfile profile)
        {
            logger.Info($"Profile changed on master. userId={profile.UserId}, dirtyProperties={profile.HasDirtyProperties}, clientConnected={profile.ClientPeer?.IsConnected}",
                LogChannels.Economy);
            QueueProfileToSave(profile);

            if (profilesInitialLoadInProgress.ContainsKey(profile.UserId))
                return;

            SendUpdatesToClient(profile);
        }

        /// <summary>
        /// Queues a profile to be saved to the database after the configured delay.
        /// </summary>
        /// <param name="profile"></param>
        protected void QueueProfileToSave(ObservableServerProfile profile)
        {
            var pendingSave = new PendingProfileSave(profile);

            lock (persistenceLifecycleSync)
            {
                if (!acceptsPersistenceOperations)
                {
                    throw new InvalidOperationException(
                        $"Profile persistence is stopping. UserId={profile.UserId}");
                }

                profilesListToSave.AddOrUpdate(
                    profile.UserId, pendingSave, (key, oldValue) => pendingSave);
            }
        }

        private bool TryQueueProfileToSave(ObservableServerProfile profile, long runGeneration)
        {
            var pendingSave = new PendingProfileSave(profile);

            lock (persistenceLifecycleSync)
            {
                if (!acceptsPersistenceOperations || persistenceRunGeneration != runGeneration)
                    return false;

                profilesListToSave.AddOrUpdate(
                    profile.UserId, pendingSave, (key, oldValue) => pendingSave);
                return true;
            }
        }

        private void ClosePersistenceOperations()
        {
            lock (persistenceLifecycleSync)
            {
                acceptsPersistenceOperations = false;
                persistenceRunGeneration++;

                foreach (ObservableServerProfile profile in profilesList.Values)
                    profile.UnloadDebounceDispatcher?.Cancel();
            }
        }

        private void EnsurePersistenceOperationsAllowed()
        {
            lock (persistenceLifecycleSync)
            {
                if (!acceptsPersistenceOperations)
                    throw new InvalidOperationException("Profile persistence is stopping");
            }
        }

        /// <summary>
        /// Collects changes in the profile, and sends them to client after delay
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        protected void SendUpdatesToClient(ObservableServerProfile profile)
        {
            if (profile.ClientPeer == null || !profile.ClientPeer.IsConnected)
            {
                logger.Warn($"Profile update to client skipped. reason=client_not_connected, userId={profile.UserId}, dirtyProperties={profile.HasDirtyProperties}",
                    LogChannels.Economy);
                // If client is not connected, and we don't need to send him profile updates
                profile.ClearUpdates();
                return;
            }

            profilesListToSend.AddOrUpdate(profile.UserId, profile, (key, oldValue) => profile);
        }

        /// <summary>
        /// Unloads profile after a period of time
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        protected void UnloadProfile(string userId)
        {
            if (profilesList.TryGetValue(userId, out ObservableServerProfile profile) && profile != null)
            {
                lock (persistenceLifecycleSync)
                {
                    if (!acceptsPersistenceOperations)
                        return;

                    long runGeneration = persistenceRunGeneration;
                    profile.UnloadDebounceDispatcher.Cancel();
                    profile.UnloadDebounceDispatcher.Debounce(() =>
                    {
                        lock (profile)
                        {
                            if (authModule.IsUserLoggedInById(userId) ||
                                !profilesList.TryGetValue(userId, out ObservableServerProfile currentProfile) ||
                                !ReferenceEquals(currentProfile, profile))
                            {
                                return;
                            }

                            profilesPendingUnload.AddOrUpdate(userId, profile, (key, oldValue) => profile);

                            if (!TryQueueProfileToSave(profile, runGeneration))
                                TryRemoveExact(profilesPendingUnload, userId, profile);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Check if given peer has permission to edit profile
        /// </summary>
        /// <param name="messagePeer"></param>
        /// <returns></returns>
        protected virtual bool HasPermissionToEditProfiles(IPeer messagePeer)
        {
            var securityExtension = messagePeer.GetExtension<SecurityInfoPeerExtension>();

            return securityExtension != null &&
                   (securityExtension.HasPermission(MstPermissionKeys.RoomServer) ||
                    securityExtension.HasAccountPermission(MstPermissionLevels.Admin));
        }

        #region INCOMMING MESSAGES

        /// <summary>
        /// Handles a message from game server, which includes player profiles updates
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task ServerUpdateProfileValuesHandler(IIncomingMessage message)
        {
            if (message == null)
                return;

            if (!HasPermissionToEditProfiles(message.Peer))
            {
                logger.Error("Master server received an update for a profile, but peer who tried to " +
                           "update it did not have sufficient permissions");

                if (message.IsExpectingResponse)
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.PERMISSION_DENIED);

                return;
            }

            if (!message.HasData)
            {
                logger.Error("Received empty message for profile update");

                if (message.IsExpectingResponse)
                    message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.PROFILE_UPDATE_EMPTY);

                return;
            }

            List<KeyValuePair<string, byte[]>> profileUpdates;

            try
            {
                byte[] data = message.AsBytes();
                logger.Info($"Master received profile update payload. payloadBytes={data.Length}",
                    LogChannels.Economy);
                profileUpdates = ReadProfileUpdates(data);
            }
            catch (Exception exception)
            {
                logger.Error($"Invalid profile update payload: {exception}",
                    LogChannels.Economy);

                if (message.IsExpectingResponse)
                    message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.PROFILE_UPDATE_INVALID);

                return;
            }

            if (message.IsExpectingResponse && profileUpdates.Count != 1)
            {
                logger.Error($"Confirmed profile save must contain exactly one profile. profiles={profileUpdates.Count}",
                    LogChannels.Economy);
                var properties = new MstProperties();
                properties.Set(MstErrorPropertyKeys.COUNT, profileUpdates.Count);
                message.RespondError(ResponseStatus.BadRequest,
                    MstErrorCodes.PROFILE_SAVE_BATCH_INVALID, properties);
                return;
            }

            foreach (KeyValuePair<string, byte[]> profileUpdate in profileUpdates)
            {
                bool applied;

                try
                {
                    applied = await ApplyProfileUpdatesAsync(profileUpdate.Key,
                        profileUpdate.Value, message.IsExpectingResponse,
                        profileLifecycleCancellation.Token);
                }
                catch (OperationCanceledException) when (
                    profileLifecycleCancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.Error($"Profile update failed. userId={profileUpdate.Key}, error={exception}",
                        LogChannels.Economy);

                    if (message.IsExpectingResponse)
                    {
                        message.RespondError(ResponseStatus.ServiceUnavailable,
                            MstErrorCodes.PROFILE_SAVE_UNAVAILABLE);
                        return;
                    }

                    continue;
                }

                if (applied)
                    continue;

                logger.Warn($"Profile update skipped on master. reason=profile_not_loaded, userId={profileUpdate.Key}, updateBytes={profileUpdate.Value.Length}",
                    LogChannels.Economy);

                if (message.IsExpectingResponse)
                {
                    message.RespondError(ResponseStatus.NotFound,
                        MstErrorCodes.USER_PROFILE_NOT_FOUND);
                    return;
                }
            }

            if (message.IsExpectingResponse)
                message.Respond(ResponseStatus.Success);
        }

        private List<KeyValuePair<string, byte[]>> ReadProfileUpdates(byte[] data)
        {
            var result = new List<KeyValuePair<string, byte[]>>();
            var userIds = new HashSet<string>(StringComparer.Ordinal);
            int safeMaxUpdateSize = Math.Max(1,
                Math.Min(maxUpdateSize,
                    MstNetworkLimits.MaxMessagePayloadByteCount));

            using (var stream = new MemoryStream(data))
            using (var reader = new EndianBinaryReader(
                EndianBitConverter.Big, stream))
            {
                int count = reader.ReadCount32(
                    MstNetworkLimits.MaxProfilePropertyCount,
                    "Profile update batch");

                for (int i = 0; i < count; i++)
                {
                    string userId = reader.ReadString();

                    if (string.IsNullOrWhiteSpace(userId))
                        throw new InvalidDataException("Profile update user id is empty");

                    if (!userIds.Add(userId))
                        throw new InvalidDataException(
                            $"Profile update contains duplicate user id '{userId}'");

                    int updatesLength = reader.ReadLength32(
                        MstNetworkLimits.MaxMessagePayloadByteCount,
                        $"Profile update for {userId}");

                    if (updatesLength == 0 || updatesLength >= safeMaxUpdateSize)
                    {
                        throw new InvalidDataException(
                            $"Profile update for {userId} has invalid size: {updatesLength} bytes");
                    }

                    byte[] updates = reader.ReadBytesExact(updatesLength,
                        safeMaxUpdateSize - 1);
                    result.Add(new KeyValuePair<string, byte[]>(userId,
                        updates));
                }

                if (stream.Position != stream.Length)
                    throw new InvalidDataException(
                        "Profile update payload contains trailing data");
            }

            logger.Info($"Master profile update payload parsed. profiles={result.Count}",
                LogChannels.Economy);
            return result;
        }

        private async Task<bool> ApplyProfileUpdatesAsync(string userId,
            byte[] updates, bool saveImmediately,
            CancellationToken cancellationToken)
        {
            SemaphoreSlim operationSemaphore = GetProfileOperationSemaphore(userId);
            await operationSemaphore.WaitAsync(cancellationToken);

            try
            {
                if (!profilesList.TryGetValue(userId,
                    out ObservableServerProfile profile))
                {
                    return false;
                }

                lock (profile)
                {
                    if (!profilesList.TryGetValue(userId,
                            out ObservableServerProfile currentProfile) ||
                        !ReferenceEquals(currentProfile, profile))
                    {
                        return false;
                    }

                    logger.Info($"Applying profile update on master. userId={userId}, updateBytes={updates.Length}, confirmedSave={saveImmediately}",
                        LogChannels.Economy);
                    profile.ApplyUpdates(updates);
                }

                if (saveImmediately)
                    await SaveProfileCoreAsync(profile, cancellationToken);

                return true;
            }
            finally
            {
                operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Handles a request from client to get profile
        /// </summary>
        /// <param name="message"></param>
        /// <param name="serverCancellationToken">Cancellation token for the current server run.</param>
        protected virtual async Task ClientFillInProfileValuesRequestHandler(IIncomingMessage message,
            CancellationToken serverCancellationToken)
        {
            serverCancellationToken.ThrowIfCancellationRequested();
            var user = message.Peer.GetExtension<IUserPeerExtension>();

            if (user == null)
            {
                serverCancellationToken.ThrowIfCancellationRequested();
                logger.Error($"User is not logged in");
                message.RespondError(ResponseStatus.Unauthorized,
                    MstErrorCodes.USER_IS_NOT_LOGGED_IN);
                return;
            }

            ProfilePeerExtension profileExt = null;
            int cancellationDelay = profileLoadTimeoutSeconds * 1000;
            using var timeoutCancellation = new CancellationTokenSource(cancellationDelay);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                serverCancellationToken,
                timeoutCancellation.Token);
            CancellationToken cancellationToken = linkedCancellation.Token;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                while (!message.Peer.TryGetExtension(out profileExt))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!authModule.IsUserLoggedInById(user.UserId))
                        throw new UnauthorizedAccessException("User logged out during profile fetch");

                    await Task.Delay(100, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                profileExt.Profile.ClientPeer = message.Peer;
                cancellationToken.ThrowIfCancellationRequested();
                message.Respond(profileExt.Profile.ToBytes(), ResponseStatus.Success);
            }
            catch (OperationCanceledException) when (serverCancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                serverCancellationToken.ThrowIfCancellationRequested();
                logger.Error(ex.Message);
                message.RespondError(ResponseStatus.Unauthorized,
                    MstErrorCodes.PROFILE_FETCH_SESSION_ENDED);
            }
            catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
            {
                serverCancellationToken.ThrowIfCancellationRequested();
                logger.Error($"Profile fetch timeout for user {user.UserId}");
                message.RespondError(ResponseStatus.Timeout, MstErrorCodes.PROFILE_FETCH_TIMEOUT);
            }
            catch (Exception ex)
            {
                serverCancellationToken.ThrowIfCancellationRequested();
                logger.Error($"Unexpected error during profile fetch for user {user.UserId}: {ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.PROFILE_FETCH_FAILED);
            }
        }


        /// <summary>
        /// Handles a request from game server to get a profile
        /// </summary>
        /// <param name="message"></param>
        /// <param name="serverCancellationToken">Cancellation token for the current server run.</param>
        protected virtual async Task ServerFillInProfileValuesRequestHandler(IIncomingMessage message,
            CancellationToken serverCancellationToken)
        {
            serverCancellationToken.ThrowIfCancellationRequested();

            if (!HasPermissionToEditProfiles(message.Peer))
            {
                serverCancellationToken.ThrowIfCancellationRequested();
                logger.Error("Master server received a request to get a profile, but peer who tried to " +
                           "update it did not have sufficient permissions");
                message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.PERMISSION_DENIED);
                return;
            }

            var userId = message.AsString();
            ObservableServerProfile profile = null;
            int cancellationDelay = profileLoadTimeoutSeconds * 1000;
            using var timeoutCancellation = new CancellationTokenSource(cancellationDelay);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                serverCancellationToken,
                timeoutCancellation.Token);
            CancellationToken cancellationToken = linkedCancellation.Token;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                while (!profilesList.TryGetValue(userId, out profile))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!authModule.IsUserLoggedInById(userId))
                        throw new UnauthorizedAccessException("User logged out during profile fetch");

                    await Task.Delay(100, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                byte[] rawProfile = profile.ToBytes();
                cancellationToken.ThrowIfCancellationRequested();
                message.Respond(rawProfile, ResponseStatus.Success);
            }
            catch (OperationCanceledException) when (serverCancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                serverCancellationToken.ThrowIfCancellationRequested();
                logger.Error(ex.Message);
                message.RespondError(ResponseStatus.Unauthorized,
                    MstErrorCodes.PROFILE_FETCH_SESSION_ENDED);
            }
            catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
            {
                serverCancellationToken.ThrowIfCancellationRequested();
                logger.Error($"Profile fetch timeout for user {userId}");
                message.RespondError(ResponseStatus.Timeout, MstErrorCodes.PROFILE_FETCH_TIMEOUT);
            }
            catch (Exception ex)
            {
                serverCancellationToken.ThrowIfCancellationRequested();
                logger.Error($"Unexpected error during profile fetch for user {userId}: {ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.PROFILE_FETCH_FAILED);
            }
        }

        #endregion

        /// <summary>
        /// Creates an observable profile for a client.
        /// Override this, if you want to customize the profile creation
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="clientPeer"></param>
        /// <returns></returns>
        public virtual ObservableServerProfile CreateProfile(string userId, IPeer clientPeer = null)
        {
            var profile = new ObservableServerProfile(userId, clientPeer);

            foreach (var populator in populatorsDatabase)
            {
                profile.Add(populator.Populate());
            }

            return profile;
        }

        /// <summary>
        /// Gets user profile by userId
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public ObservableServerProfile GetProfileByUserId(string userId)
        {
            profilesList.TryGetValue(userId, out ObservableServerProfile profile);
            return profile;
        }

        /// <summary>
        /// Gets user profile by peer
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public ObservableServerProfile GetProfileByPeer(IPeer peer)
        {
            var user = peer.GetExtension<IUserPeerExtension>();
            if (user == null) return null;
            return GetProfileByUserId(user.UserId);
        }
    }
}
