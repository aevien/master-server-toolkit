using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public delegate ObservableServerProfile ObservableProfileFactoryDelegate(string userId, IPeer clientPeer);

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
        [SerializeField, Tooltip("If true, chat module will subscribe to auth module, and automatically setup chat users when they log in")]
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
        /// Ignore errors occurred when profile data mismatch
        /// </summary>
        [Tooltip("Ignore errors occurred when profile data mismatch")]
        public bool ignoreProfileMissmatchError = false;

        /// <summary>
        /// Database accessor factory that helps to create integration with profile db
        /// </summary>
        [Tooltip("Database accessor factory that helps to create integration with profile db")]
        public DatabaseAccessorFactory databaseAccessorFactory;

        #endregion

        private float timeToWaitProfile = 10f * 1000f;

        /// <summary>
        /// Auth module for listening to auth events
        /// </summary>
        protected AuthModule authModule;

        /// <summary>
        /// List of profiles that will be saved to to DB with updates
        /// </summary>
        protected readonly HashSet<string> profilesToBeSaved = new HashSet<string>();

        /// <summary>
        /// List of profiles that will be sent to clients with updates
        /// </summary>
        protected readonly HashSet<string> profilesToBeSentToClients = new HashSet<string>();

        /// <summary>
        /// DB to work with profile data
        /// </summary>
        protected IProfilesDatabaseAccessor profileDatabaseAccessor;

        /// <summary>
        /// List of the users profiles
        /// </summary>
        protected readonly ConcurrentDictionary<string, ObservableServerProfile> profilesList = new ConcurrentDictionary<string, ObservableServerProfile>();

        /// <summary>
        /// By default, profiles module will use this factory to create a profile for users.
        /// If you're using profiles, you will need to change this factory to construct the
        /// structure of a profile.
        /// </summary>
        public ObservableProfileFactoryDelegate ProfileFactory { get; set; }

        /// <summary>
        /// Gets list of userprofiles
        /// </summary>
        public IEnumerable<ObservableServerProfile> Profiles => profilesList.Values;

        /// <summary>
        /// Ignore errors occurred when profile data mismatch. False by default
        /// </summary>
        public bool IgnoreProfileMissmatchError
        {
            get { return ignoreProfileMissmatchError; }
            set { ignoreProfileMissmatchError = value; }
        }

        protected override void Awake()
        {
            base.Awake();

            // Add auth module as a dependency of this module
            AddOptionalDependency<AuthModule>();
        }

        public override void Initialize(IServer server)
        {
            if (databaseAccessorFactory)
                databaseAccessorFactory.CreateAccessors();

            profileDatabaseAccessor = Mst.Server.DbAccessors.GetAccessor<IProfilesDatabaseAccessor>();

            if (profileDatabaseAccessor == null)
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
                }
                else
                {
                    logger.Error($"{GetType().Name} was set to use {nameof(AuthModule)}, but {nameof(AuthModule)} was not found");
                }
            }

            server.RegisterMessageHandler(MstOpCodes.ServerFillInProfileValues, GameServerProfileRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerUpdateProfileValues, ServerUpdateProfileValuesHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientFillInProfileValues, ClientFillInProfileValuesHandler);
        }

        public override MstProperties Info()
        {
            MstProperties info = base.Info();

            info.Add("Database Accessor", profileDatabaseAccessor != null ? "Connected" : "Not Connected");
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
            user.Peer.OnConnectionCloseEvent += OnPeerPlayerDisconnectedEventHandler;

            // Create a profile
            ObservableServerProfile profile;

            if (profilesList.ContainsKey(user.UserId))
            {
                // There's a profile from before, which we can use
                profile = profilesList[user.UserId];
                profile.ClientPeer = user.Peer;
            }
            else
            {
                // We need to create a new one
                profile = CreateProfile(user.UserId, user.Peer);
                profilesList.TryAdd(user.UserId, profile);

                // Restore profile data from database
                await profileDatabaseAccessor.RestoreProfileAsync(profile);

                // Listen to profile events
                profile.OnModifiedInServerEvent += OnProfileChangedEventHandler;
            }

            // 
            profile.ClearUpdates();

            // Save profile property
            user.Peer.AddExtension(new ProfilePeerExtension(profile, user.Peer));
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
            if (ProfileFactory != null)
                return ProfileFactory.Invoke(userId, clientPeer);

            return new ObservableServerProfile(userId, clientPeer);
        }

        /// <summary>
        /// Invoked, when profile is changed
        /// </summary>
        /// <param name="profile"></param>
        protected virtual void OnProfileChangedEventHandler(ObservableServerProfile profile)
        {
            var user = profile.ClientPeer.GetExtension<IUserPeerExtension>();

            if (!user.Account.IsGuest || (user.Account.IsGuest && authModule.SaveGuestInfo))
            {
                lock (profilesToBeSaved)
                {
                    if (!profilesToBeSaved.Contains(profile.UserId) && profile.ShouldBeSavedToDatabase)
                    {
                        // If profile is not already waiting to be saved
                        profilesToBeSaved.Add(profile.UserId);
                        SaveProfile(profile, saveProfileDebounceTime);
                    }
                }
            }

            lock (profilesToBeSentToClients)
            {
                if (!profilesToBeSentToClients.Contains(profile.UserId))
                {
                    // If it's a master server
                    profilesToBeSentToClients.Add(profile.UserId);
                    SendUpdatesToClient(profile, clientUpdateDebounceTime);
                }
            }
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
                _ = UnloadProfile(profileExtension.UserId, unloadProfileAfter);
            }
        }

        /// <summary>
        /// Saves a profile into database after delay
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        protected async void SaveProfile(ObservableServerProfile profile, float delay)
        {
            // Wait for the delay
            await Task.Delay(Mathf.RoundToInt(delay < 0.01f ? 0.01f * 1000 : delay * 1000));

            lock (profilesToBeSaved)
            {
                // Remove value from debounced updates
                profilesToBeSaved.Remove(profile.UserId);
            }

            await profileDatabaseAccessor.UpdateProfileAsync(profile);
        }

        /// <summary>
        /// Collects changes in the profile, and sends them to client after delay
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        protected async void SendUpdatesToClient(ObservableServerProfile profile, float delay)
        {
            // Wait for the delay
            await Task.Delay(Mathf.RoundToInt(delay < 0.01f ? 0.01f * 1000 : delay * 1000));

            if (profile.ClientPeer == null || !profile.ClientPeer.IsConnected)
            {
                // If client is not connected, and we don't need to send him profile updates
                profile.ClearUpdates();

                lock (profilesToBeSentToClients)
                {
                    // Remove value from debounced updates
                    profilesToBeSentToClients.Remove(profile.UserId);
                }

                return;
            }

            // Get profile updated data in bytes
            var updates = profile.GetUpdates();

            // Clear updated data in profile
            profile.ClearUpdates();

            // Send these data to client
            profile.ClientPeer.SendMessage(MessageHelper.Create(MstOpCodes.UpdateClientProfile, updates), DeliveryMethod.ReliableSequenced);

            lock (profilesToBeSentToClients)
            {
                // Remove value from debounced updates
                profilesToBeSentToClients.Remove(profile.UserId);
            }
        }

        /// <summary>
        /// Unloads profile after a period of time
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        protected async Task UnloadProfile(string userId, float delay)
        {
            // Wait for the delay
            await Task.Delay(Mathf.RoundToInt(delay < 0.01f ? 0.01f * 1000 : delay * 1000));

            // If user is logged in, do nothing
            if (authModule.IsUserLoggedInById(userId))
                return;

            if (profilesList.TryRemove(userId, out ObservableServerProfile profile) && profile != null)
                profile.OnModifiedInServerEvent -= OnProfileChangedEventHandler;
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
        protected virtual void ServerUpdateProfileValuesHandler(IIncomingMessage message)
        {
            if (!HasPermissionToEditProfiles(message.Peer))
            {
                Logs.Error("Master server received an update for a profile, but peer who tried to " +
                           "update it did not have sufficient permissions");
                return;
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
        }

        /// <summary>
        /// Handles a request from client to get profile
        /// </summary>
        /// <param name="message"></param>
        protected virtual async void ClientFillInProfileValuesHandler(IIncomingMessage message)
        {
            int totalTime = 0;
            ProfilePeerExtension profileExt = null;

            // Wait for user profile
            while (totalTime < timeToWaitProfile)
            {
                await Task.Delay(100);
                totalTime += 100;

                if (message.Peer.TryGetExtension(out profileExt))
                    break;
            }

            if (profileExt == null)
            {
                message.Respond(ResponseStatus.NotFound);
                return;
            }

            var clientPropertiesCount = message.AsInt();

            profileExt.Profile.ClientPeer = message.Peer;

            if (!ignoreProfileMissmatchError && clientPropertiesCount != profileExt.Profile.Count)
            {
                logger.Error(string.Format($"Client requested a profile with {clientPropertiesCount} properties, but server " +
                                           $"constructed a profile with {profileExt.Profile.Count}. Make sure that you've changed the " +
                                           "profile factory on the ProfilesModule"));
            }

            message.Respond(profileExt.Profile.ToBytes(), ResponseStatus.Success);
        }

        /// <summary>
        /// Handles a request from game server to get a profile
        /// </summary>
        /// <param name="message"></param>
        protected virtual void GameServerProfileRequestHandler(IIncomingMessage message)
        {
            if (!HasPermissionToEditProfiles(message.Peer))
            {
                message.Respond("Invalid permission level", ResponseStatus.Unauthorized);
                return;
            }

            var userId = message.AsString();

            profilesList.TryGetValue(userId, out ObservableServerProfile profile);

            if (profile == null)
            {
                message.Respond(ResponseStatus.Failed);
                return;
            }

            message.Respond(profile.ToBytes(), ResponseStatus.Success);
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