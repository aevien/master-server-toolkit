using MasterServerToolkit.Networking;
using static MasterServerToolkit.MasterServer.ObservableProfile;
using MasterServerToolkit.Logging;

namespace MasterServerToolkit.MasterServer
{
    public class ProfilesClient : MstBaseClient
    {
        private ObservableProfileDelegate profileLoadEvent;

        /// <summary>
        /// Currently loaded user profile
        /// </summary>
        public ObservableProfile Current { get; private set; }

        /// <summary>
        /// Checks if profile exists
        /// </summary>
        public bool IsLoaded => Current != null;

        /// <summary>
        /// Invoked when profile loaded
        /// </summary>
        public event ObservableProfileDelegate OnProfileLoadedEvent
        {
            add
            {
                profileLoadEvent += value;

                if (IsLoaded)
                    value?.Invoke(Current);
            }
            remove
            {
                profileLoadEvent -= value;
            }
        }

        public ProfilesClient(IClientSocket connection) : base(connection)
        {
            RegisterErrorParsers();
        }

        /// <summary>
        /// Stops receiving updates for the current profile and releases its local state.
        /// </summary>
        public void UnloadProfile()
        {
            Connection?.UnregisterMessageHandler(MstOpCodes.UpdateClientProfile);

            ObservableProfile profile = Current;
            Current = null;
            profile?.Dispose();
        }

        internal static void RegisterErrorParsers()
        {
            Mst.Errors.TryRegister(MstErrorCodes.PERMISSION_DENIED);
            Mst.Errors.TryRegister(MstErrorCodes.USER_IS_NOT_LOGGED_IN);
            Mst.Errors.TryRegister(MstErrorCodes.PROFILE_UPDATE_EMPTY);
            Mst.Errors.TryRegister(MstErrorCodes.PROFILE_UPDATE_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.PROFILE_SAVE_BATCH_INVALID, properties =>
                Mst.Errors.LocalizeFormat("ui.error.profiles.save_batch_invalid.message",
                    properties.AsInt(MstErrorPropertyKeys.COUNT)));
            Mst.Errors.TryRegister(MstErrorCodes.PROFILE_SAVE_UNAVAILABLE);
            Mst.Errors.TryRegister(MstErrorCodes.USER_PROFILE_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.PROFILE_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.PROFILE_FETCH_SESSION_ENDED);
            Mst.Errors.TryRegister(MstErrorCodes.PROFILE_FETCH_TIMEOUT);
            Mst.Errors.TryRegister(MstErrorCodes.PROFILE_FETCH_FAILED);
        }

        /// <summary>
        /// Sends a request to server, retrieves all profile values, and applies them to a provided
        /// profile
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="callback"></param>
        public void FillInProfileValues(ObservableProfile profile, SuccessCallback callback)
        {
            FillInProfileValues(profile, callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, retrieves all profile values, and applies them to a provided profile
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void FillInProfileValues(ObservableProfile profile, SuccessCallback callback, IClientSocket connection)
        {
            bool isCompleted = false;

            void Complete(bool isSuccessful, string error)
            {
                if (isCompleted)
                    return;

                isCompleted = true;
                callback.Invoke(isSuccessful, error);
            }

            if (!connection.IsConnected)
            {
                Complete(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (!Mst.Client.Auth.IsSignedIn)
            {
                Complete(false, Mst.Errors.Parse(ResponseStatus.Unauthorized));
                return;
            }

            try
            {
                connection.SendMessage(MstOpCodes.ClientFillInProfileValues, profile.Count, (status, response) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        Complete(false, Mst.Errors.Parse(status, response));
                        return;
                    }

                    try
                    {
                        // Use the bytes received, to replicate the profile
                        profile.FromBytes(response.AsBytes());
                        profile.ClearUpdates();

                        // Listen to profile updates, and apply them
                        connection.UnregisterMessageHandler(MstOpCodes.UpdateClientProfile);
                        connection.RegisterMessageHandler(MstOpCodes.UpdateClientProfile, message =>
                        {
                            byte[] updates = message.AsBytes();
                            Logs.Info($"Client received profile update. updateBytes={updates.Length}", LogChannels.Economy);
                            profile.ApplyUpdates(updates);
                            profile.ClearUpdates();
                        });

                        if (Current != null && !ReferenceEquals(Current, profile))
                            Current.Dispose();

                        Current = profile;
                    }
                    catch (System.Exception ex)
                    {
                        Logs.Error($"Client profile response processing failed. error={ex}", LogChannels.Economy);
                        Complete(false, Mst.Errors.Parse(ResponseStatus.Error));
                        return;
                    }

                    Complete(true, null);
                    profileLoadEvent?.Invoke(profile);
                });
            }
            catch (System.Exception ex)
            {
                if (!isCompleted)
                    Logs.Error($"Client profile request failed. error={ex}", LogChannels.Economy);

                Complete(false, Mst.Errors.Parse(
                    connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected));
            }
        }
    }
}
