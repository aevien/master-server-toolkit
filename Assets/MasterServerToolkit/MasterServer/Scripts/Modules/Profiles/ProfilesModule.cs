using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public delegate ObservableServerProfile ProfileFactory(string username, IPeer clientPeer);

    /// <summary>
    /// Handles player profiles within master server.
    /// Listens to changes in player profiles, and sends updates to
    /// clients of interest.
    /// Also, reads changes from game server, and applies them to players profile
    /// </summary>
    public class ProfilesModule : BaseServerModule
    {
        #region INSPECTOR

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
        public float saveProfileInterval = 1f;

        /// <summary>
        /// Interval, in which profile updates will be sent to clients
        /// </summary>
        [Tooltip("Interval, in which profile updates will be sent to clients")]
        public float clientUpdateInterval = 0f;

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

        #endregion

        /// <summary>
        /// Auth module for listening to auth events
        /// </summary>
        protected AuthModule authModule;

        /// <summary>
        /// List of profiles that will be saved to to DB with updates
        /// </summary>
        protected HashSet<string> profilesToBeSaved;

        /// <summary>
        /// List of profiles that will be sent to clients with updates
        /// </summary>
        protected HashSet<string> profilesToBeSentToClients;

        /// <summary>
        /// DB to work with profile data
        /// </summary>
        protected IProfilesDatabaseAccessor profileDatabaseAccessor;

        /// <summary>
        /// By default, profiles module will use this factory to create a profile for users.
        /// If you're using profiles, you will need to change this factory to construct the
        /// structure of a profile.
        /// </summary>
        public ProfileFactory ProfileFactory { get; set; }

        /// <summary>
        /// List of the users profiles
        /// </summary>
        public Dictionary<string, ObservableServerProfile> ProfilesList { get; protected set; }

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

            if (DestroyIfExists())
            {
                return;
            }

            // Add auth module as a dependency of this module
            AddOptionalDependency<AuthModule>();

            // List of oaded profiles
            ProfilesList = new Dictionary<string, ObservableServerProfile>();

            // List of profiles that are waiting to be saved to DB
            profilesToBeSaved = new HashSet<string>();

            // List of profiles that are waiting to be sent to clients
            profilesToBeSentToClients = new HashSet<string>();
        }

        public override void Initialize(IServer server)
        {
            profileDatabaseAccessor = Mst.Server.DbAccessors.GetAccessor<IProfilesDatabaseAccessor>();

            if (profileDatabaseAccessor == null)
            {
                logger.Error("Profiles database implementation was not found");
            }

            // Auth dependency setup
            authModule = server.GetModule<AuthModule>();

            if (authModule != null)
            {
                authModule.OnUserLoggedInEvent += OnUserLoggedInEventHandler;
            }

            // Games dependency setup
            server.SetHandler((short)MstMessageCodes.ServerProfileRequest, GameServerProfileRequestHandler);
            server.SetHandler((short)MstMessageCodes.UpdateServerProfile, ProfileUpdateHandler);
            server.SetHandler((short)MstMessageCodes.ClientProfileRequest, ClientProfileRequestHandler);
        }

        /// <summary>
        /// Invoked, when user logs into the master server
        /// </summary>
        /// <param name="session"></param>
        /// <param name="accountData"></param>
        private async void OnUserLoggedInEventHandler(IUserPeerExtension user)
        {
            user.Peer.OnPeerDisconnectedEvent += OnPeerPlayerDisconnectedEventHandler;

            // Create a profile
            ObservableServerProfile profile;

            if (ProfilesList.ContainsKey(user.Username))
            {
                // There's a profile from before, which we can use
                profile = ProfilesList[user.Username];
                profile.ClientPeer = user.Peer;
            }
            else
            {
                // We need to create a new one
                profile = CreateProfile(user.Username, user.Peer);
                ProfilesList.Add(user.Username, profile);
            }

            // Restore profile data from database (only if not a guest)
            if (!user.Account.IsGuest)
            {
                await profileDatabaseAccessor.RestoreProfileAsync(profile);
            }

            // Save profile property
            user.Peer.AddExtension(new ProfilePeerExtension(profile, user.Peer));

            // Listen to profile events
            profile.OnModifiedInServerEvent += OnProfileChangedEventHandler;
        }

        /// <summary>
        /// Creates an observable profile for a client.
        /// Override this, if you want to customize the profile creation
        /// </summary>
        /// <param name="username"></param>
        /// <param name="clientPeer"></param>
        /// <returns></returns>
        protected virtual ObservableServerProfile CreateProfile(string username, IPeer clientPeer)
        {
            if (ProfileFactory != null)
            {
                return ProfileFactory(username, clientPeer);
            }

            return new ObservableServerProfile(username, clientPeer);
        }

        /// <summary>
        /// Invoked, when profile is changed
        /// </summary>
        /// <param name="profile"></param>
        private void OnProfileChangedEventHandler(ObservableServerProfile profile)
        {
            // Debouncing is used to reduce a number of updates per interval to one
            // TODO make debounce lookup more efficient than using string hashet

            if (!profilesToBeSaved.Contains(profile.Username) && profile.ShouldBeSavedToDatabase)
            {
                // If profile is not already waiting to be saved
                profilesToBeSaved.Add(profile.Username);
                SaveProfile(profile, saveProfileInterval);
            }

            if (!profilesToBeSentToClients.Contains(profile.Username))
            {
                // If it's a master server
                profilesToBeSentToClients.Add(profile.Username);
                SendUpdatesToClient(profile, clientUpdateInterval);
            }
        }

        /// <summary>
        /// Invoked, when user logs out (disconnects from master)
        /// </summary>
        /// <param name="session"></param>
        private void OnPeerPlayerDisconnectedEventHandler(IPeer peer)
        {
            peer.OnPeerDisconnectedEvent -= OnPeerPlayerDisconnectedEventHandler;

            var profileExtension = peer.GetExtension<ProfilePeerExtension>();

            if (profileExtension == null)
            {
                return;
            }

            // Unload profile
            UnloadProfile(profileExtension.Username, unloadProfileAfter);
        }

        /// <summary>
        /// Saves a profile into database after delay
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private async void SaveProfile(ObservableServerProfile profile, float delay)
        {
            // Wait for the delay
            await Task.Delay(Mathf.RoundToInt(delay < 0.01f ? 0.01f : delay * 1000));

            // Remove value from debounced updates
            profilesToBeSaved.Remove(profile.Username);

            await profileDatabaseAccessor.UpdateProfileAsync(profile);
        }

        /// <summary>
        /// Collects changes in the profile, and sends them to client after delay
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private async void SendUpdatesToClient(ObservableServerProfile profile, float delay)
        {
            // Wait for the delay
            await Task.Delay(Mathf.RoundToInt(delay < 0.01f ? 0.01f : delay * 1000));

            if (profile.ClientPeer == null || !profile.ClientPeer.IsConnected)
            {
                // If client is not connected, and we don't need to send him profile updates
                profile.ClearUpdates();
                return;
            }

            // Get profile updated data in bytes
            var updates = profile.GetUpdates();

            // Clear updated data in profile
            profile.ClearUpdates();

            // Send these data to client
            profile.ClientPeer.SendMessage(MessageHelper.Create((short)MstMessageCodes.UpdateClientProfile, updates), DeliveryMethod.ReliableSequenced);

            await Task.Delay(10);

            // Remove value from debounced updates
            profilesToBeSentToClients.Remove(profile.Username);
        }

        /// <summary>
        /// Unloads profile after a period of time
        /// </summary>
        /// <param name="username"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private async void UnloadProfile(string username, float delay)
        {
            // Wait for the delay
            await Task.Delay(Mathf.RoundToInt(delay < 0.01f ? 0.01f : delay * 1000));

            // If user is not actually logged in, remove the profile
            if (authModule.IsUserLoggedIn(username))
            {
                return;
            }

            ProfilesList.TryGetValue(username, out ObservableServerProfile profile);

            if (profile == null)
            {
                return;
            }

            // Remove profile
            ProfilesList.Remove(username);

            // Remove listeners
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

        #region Handlers

        /// <summary>
        /// Handles a message from game server, which includes player profiles updates
        /// </summary>
        /// <param name="message"></param>
        protected virtual void ProfileUpdateHandler(IIncommingMessage message)
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
                        // Read username
                        var username = reader.ReadString();

                        // Read updates length
                        var updatesLength = reader.ReadInt32();

                        // Read updates
                        var updates = reader.ReadBytes(updatesLength);

                        try
                        {
                            if (ProfilesList.TryGetValue(username, out ObservableServerProfile profile))
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
        protected virtual void ClientProfileRequestHandler(IIncommingMessage message)
        {
            var clientPropCount = message.AsInt();

            var profileExt = message.Peer.GetExtension<ProfilePeerExtension>();

            if (profileExt == null)
            {
                message.Respond("Profile not found", ResponseStatus.Failed);
                return;
            }

            profileExt.Profile.ClientPeer = message.Peer;

            if (!ignoreProfileMissmatchError && clientPropCount != profileExt.Profile.PropertyCount)
            {
                logger.Error(string.Format($"Client requested a profile with {clientPropCount} properties, but server " +
                                           $"constructed a profile with {profileExt.Profile.PropertyCount}. Make sure that you've changed the " +
                                           "profile factory on the ProfilesModule"));
            }

            message.Respond(profileExt.Profile.ToBytes(), ResponseStatus.Success);
        }

        /// <summary>
        /// Handles a request from game server to get a profile
        /// </summary>
        /// <param name="message"></param>
        protected virtual void GameServerProfileRequestHandler(IIncommingMessage message)
        {
            if (!HasPermissionToEditProfiles(message.Peer))
            {
                message.Respond("Invalid permission level", ResponseStatus.Unauthorized);
                return;
            }

            var username = message.AsString();

            ObservableServerProfile profile;
            ProfilesList.TryGetValue(username, out profile);

            if (profile == null)
            {
                message.Respond(ResponseStatus.Failed);
                return;
            }

            message.Respond(profile.ToBytes(), ResponseStatus.Success);
        }

        #endregion

        public ObservableServerProfile GetProfileByUsername(string username)
        {
            ObservableServerProfile profile;
            ProfilesList.TryGetValue(username, out profile);

            return profile;
        }
    }
}