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
        #region INSPECTOR

        [Header("General Settings")]
        [SerializeField, Tooltip("If true, profiles module will subscribe to auth module, and automatically setup user profile when they log in")]
        protected bool useAuthModule = true;

        /// <summary>
        /// Time to pass after logging out, until profile
        /// will be removed from the lookup. Should be enough for game
        /// server to submit last changes
        /// </summary>
        [Tooltip("Time to pass after logging out, until profile will be removed from the lookup. Should be enough for game server to submit last changes")]
        public float unloadProfileAfter = 20f;

        /// <summary>
        /// Interval, in which updated profiles will be saved to database
        /// </summary>
        [Tooltip("Interval, in which updated profiles will be saved to database")]
        public float saveProfileDebounceTime = 1f;

        /// <summary>
        /// Interval, in which profile updates will be sent to clients
        /// </summary>
        [Tooltip("Interval, in which profile updates will be sent to clients")]
        public float clientUpdateDebounceTime = .1f;

        /// <summary>
        /// Permission user need to have to edit profile
        /// </summary>
        [Tooltip("Permission user need to have to edit profile")]
        public int editProfilePermissionLevel = 0;

        /// <summary>
        /// Database accessor factory that helps to create integration with profile db
        /// </summary>
        [Tooltip("Database accessor factory that helps to create integration with profile db")]
        public DatabaseAccessorFactory databaseAccessorFactory;

        [SerializeField]
        private ObservableBasePopulator[] populators;

        #endregion

        protected readonly float timeToWaitProfile = 10f * 1000f;
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
        /// List of the users profiles
        /// </summary>
        protected readonly ConcurrentDictionary<string, ObservableServerProfile> profilesList = new ConcurrentDictionary<string, ObservableServerProfile>();

        /// <summary>
        /// 
        /// </summary>
        protected readonly ConcurrentDictionary<string, ObservableServerProfile> profilesListToSave = new ConcurrentDictionary<string, ObservableServerProfile>();

        /// <summary>
        /// List of all profile updates to be sent to clients
        /// </summary>
        protected readonly ConcurrentDictionary<string, ObservableServerProfile> profilesListToSend = new ConcurrentDictionary<string, ObservableServerProfile>();

        /// <summary>
        /// Gets list of userprofiles
        /// </summary>
        public IEnumerable<ObservableServerProfile> Profiles => profilesList.Values;

        /// <summary>
        /// It is performed after initialization of the profile structure
        /// </summary>
        public event Action<ObservableServerProfile> OnProfileCreated;

        /// <summary>
        /// It is performed after loading the profile from the database
        /// </summary>
        public event Action<ObservableServerProfile> OnProfileLoaded;

        protected override void Awake()
        {
            base.Awake();

            // Add auth module as a dependency of this module
            AddOptionalDependency<AuthModule>();

            // Set debounce dispatchers
            saveDebounceDispatcher = new ThrottleDispatcher((int)(saveProfileDebounceTime * 1000f));
            sendDebounceDispatcher = new ThrottleDispatcher((int)(clientUpdateDebounceTime * 1000f));
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

            if (useAuthModule)
            {
                if (authModule)
                {
                    authModule.OnUserLoggedInEvent += OnUserLoggedInEventHandler;
                    authModule.OnUserLoggedOutEvent += OnUserLoggedOutEvent;
                }
                else
                {
                    logger.Error($"{GetType().Name} was set to use {nameof(AuthModule)}, but {nameof(AuthModule)} was not found");
                }
            }

            server.RegisterMessageHandler(MstOpCodes.ServerFillInProfileValues, ServerFillInProfileValuesRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerUpdateProfileValues, ServerUpdateProfileValuesHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientFillInProfileValues, ClientFillInProfileValuesRequestHandler);
        }

        public override MstProperties Info()
        {
            MstProperties info = base.Info();

            info.Add("Database Accessor", databaseAccessor != null ? "Connected" : "Not Connected");
            info.Add("Profiles", Profiles?.Count());

            return info;
        }

        /// <summary>
        /// Triggered when the user has successfully logged in
        /// </summary>
        /// <param name="session"></param>
        /// <param name="accountData"></param>
        protected virtual async void OnUserLoggedInEventHandler(IUserPeerExtension user)
        {
            try
            {
                user.Peer.OnConnectionCloseEvent += OnPeerPlayerDisconnectedEventHandler;

                // Create a profile
                ObservableServerProfile profile;

                if (profilesList.ContainsKey(user.UserId))
                {
                    logger.Debug($"User {user.UserId} already contains profile in memory. Uset it");

                    // There's a profile from before, which we can use
                    profile = profilesList[user.UserId];
                    profile.ClientPeer = user.Peer;
                }
                else
                {
                    logger.Debug($"User {user.UserId} needs to create or load profile from db");

                    // We need to create a new one
                    profile = CreateProfile(user.UserId, user.Peer);
                    profile.UnloadDebounceDispatcher = new DebounceDispatcher((int)(unloadProfileAfter * 1000f));

                    profilesList.TryAdd(user.UserId, profile);

                    // Restore profile data from database
                    await databaseAccessor.RestoreProfileAsync(profile);

                    // Listen to profile events
                    profile.OnModifiedInServerEvent += OnProfileChangedEventHandler;

                    OnProfileLoaded?.Invoke(profile);
                }

                // 
                profile.ClearUpdates();

                // Save profile property
                user.Peer.AddExtension(new ProfilePeerExtension(profile, user.Peer));
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        protected virtual void OnUserLoggedOutEvent(IUserPeerExtension user)
        {
            user.Peer.ClearExtension<ProfilePeerExtension>();
        }

        /// <summary>
        /// Creates an observable profile for a client.
        /// Override this, if you want to customize the profile creation
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="clientPeer"></param>
        /// <returns></returns>
        protected virtual ObservableServerProfile CreateProfile(string userId, IPeer clientPeer)
        {
            var profile = new ObservableServerProfile(userId, clientPeer);

            foreach (var populator in populators)
            {
                profile.Add(populator.Populate());
            }

            OnProfileCreated?.Invoke(profile);

            return profile;
        }

        /// <summary>
        /// Invoked, when profile is changed
        /// </summary>
        /// <param name="profile"></param>
        protected virtual void OnProfileChangedEventHandler(ObservableServerProfile profile)
        {
            SaveProfile(profile);
            SendUpdatesToClient(profile);
        }

        /// <summary>
        /// Invoked, when user logs out (disconnects from master)
        /// </summary>
        /// <param name="session"></param>
        protected virtual void OnPeerPlayerDisconnectedEventHandler(IPeer peer)
        {
            peer.OnConnectionCloseEvent -= OnPeerPlayerDisconnectedEventHandler;

            var profileExtension = peer.GetExtension<ProfilePeerExtension>();

            if (profileExtension != null)
            {
                // Unload profile
                UnloadProfile(profileExtension.UserId);
            }
        }

        /// <summary>
        /// Saves a profile into database after delay
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        protected void SaveProfile(ObservableServerProfile profile)
        {
            profilesListToSave.TryAdd(profile.UserId, profile);

            saveDebounceDispatcher.ThrottleAsync(async () =>
            {
                var snapshot = profilesListToSave.Values.ToList();
                profilesListToSave.Clear();

                await databaseAccessor.UpdateProfilesAsync(snapshot);
            });
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
                // If client is not connected, and we don't need to send him profile updates
                profile.ClearUpdates();
                return;
            }

            profilesListToSend.TryAdd(profile.UserId, profile);

            sendDebounceDispatcher.Throttle(() =>
            {
                var snapshot = new Dictionary<IPeer, byte[]>();
                snapshot = profilesListToSend.ToDictionary(k => k.Value.ClientPeer, k => k.Value.GetUpdates());
                profilesListToSend.Clear();

                foreach (var item in snapshot)
                {
                    // Send these data to client
                    if (item.Key.IsConnected)
                        item.Key.SendMessage(MessageHelper.Create(MstOpCodes.UpdateClientProfile, item.Value), DeliveryMethod.ReliableSequenced);
                }
            });
        }

        /// <summary>
        /// Unloads profile after a period of time
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        protected void UnloadProfile(string userId)
        {
            if (profilesList.TryRemove(userId, out ObservableServerProfile profile) && profile != null)
            {
                profile.UnloadDebounceDispatcher.Debounce(() =>
                {
                    // If user is logged in, do nothing
                    if (authModule.IsUserLoggedInById(userId))
                        return;

                    profile.OnModifiedInServerEvent -= OnProfileChangedEventHandler;
                });
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

            return securityExtension != null
                   && securityExtension.PermissionLevel >= editProfilePermissionLevel;
        }

        #region INCOMMING MESSAGES

        /// <summary>
        /// Handles a message from game server, which includes player profiles updates
        /// </summary>
        /// <param name="message"></param>
        protected virtual Task ServerUpdateProfileValuesHandler(IIncomingMessage message)
        {
            if (!HasPermissionToEditProfiles(message.Peer))
            {
                logger.Error("Master server received an update for a profile, but peer who tried to " +
                           "update it did not have sufficient permissions");
                return Task.CompletedTask;
            }

            var data = message.AsBytes();

            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    // Read profiles count
                    var count = reader.ReadInt32();

                    for (var i = 0; i < count; i++)
                    {
                        // Read userId
                        var userId = reader.ReadString();

                        // Read updates length
                        var updatesLength = reader.ReadInt32();

                        // Read updates
                        var updates = reader.ReadBytes(updatesLength);

                        try
                        {
                            if (profilesList.TryGetValue(userId, out ObservableServerProfile profile))
                            {
                                profile.ApplyUpdates(updates);
                            }
                        }
                        catch (Exception e)
                        {
                            Logs.Error("Error while trying to handle profile updates from master server");
                            Logs.Error(e);
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a request from client to get profile
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task ClientFillInProfileValuesRequestHandler(IIncomingMessage message)
        {
            var user = message.Peer.GetExtension<IUserPeerExtension>();

            if (user == null)
            {
                logger.Error($"User is not logged in");
                message.Respond(ResponseStatus.Unauthorized);
                return;
            }

            ProfilePeerExtension profileExt = null;
            using var cts = new CancellationTokenSource((int)timeToWaitProfile);

            try
            {
                await Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (message.Peer.TryGetExtension(out profileExt))
                        {
                            break;
                        }

                        if (!authModule.IsUserLoggedInById(user.UserId))
                        {
                            throw new UnauthorizedAccessException("User logged out during profile fetch");
                        }

                        await Task.Delay(100, cts.Token);
                    }
                }, cts.Token);

                if (profileExt == null)
                {
                    logger.Error($"Profile for user {user.UserId} not found within the timeout period");
                    message.Respond(ResponseStatus.NotFound);
                    return;
                }

                profileExt.Profile.ClientPeer = message.Peer;
                message.Respond(profileExt.Profile.ToBytes(), ResponseStatus.Success);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.Error(ex.Message);
                message.Respond("User logged out during profile fetch", ResponseStatus.Unauthorized);
            }
            catch (OperationCanceledException)
            {
                logger.Error($"Profile fetch timeout for user {user.UserId}");
                message.Respond("Timeout while waiting for profile", ResponseStatus.NotFound);
            }
            catch (Exception ex)
            {
                logger.Error($"Unexpected error during profile fetch for user {user.UserId}: {ex}");
                message.Respond(ResponseStatus.Failed);
            }
        }


        /// <summary>
        /// Handles a request from game server to get a profile
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task ServerFillInProfileValuesRequestHandler(IIncomingMessage message)
        {
            if (!HasPermissionToEditProfiles(message.Peer))
            {
                logger.Error("Master server received a request to get a profile, but peer who tried to " +
                           "update it did not have sufficient permissions");
                message.Respond(ResponseStatus.Unauthorized);
                return;
            }

            int totalTime = 0;
            var userId = message.AsString();

            ObservableServerProfile profile = null;

            // Wait for user profile
            while (totalTime < timeToWaitProfile)
            {
                await Task.Delay(100);
                totalTime += 100;

                if (profilesList.TryGetValue(userId, out profile) && profile != null)
                    break;
            }

            if (profile == null)
            {
                message.Respond(ResponseStatus.Failed);
                return;
            }

            byte[] rawProfile = profile.ToBytes();
            message.Respond(rawProfile, ResponseStatus.Success);
        }

        #endregion

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