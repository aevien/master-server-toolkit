using MasterServerToolkit.Networking;
using static MasterServerToolkit.MasterServer.ObservableProfile;

namespace MasterServerToolkit.MasterServer
{
    public class MstProfilesClient : MstBaseClient
    {
        private event ObservableProfileDelegate profileLoadEvent;

        /// <summary>
        /// Currently loaded user profile
        /// </summary>
        public ObservableProfile Current { get; private set; }

        /// <summary>
        /// Checks if profile exists
        /// </summary>
        public bool HasProfile => Current != null;

        /// <summary>
        /// Invoked when profile loaded
        /// </summary>
        public event ObservableProfileDelegate OnProfileLoadedEvent
        {
            add
            {
                if (HasProfile)
                {
                    value?.Invoke(Current);
                }
                else
                {
                    profileLoadEvent += value;
                }
            }
            remove
            {
                profileLoadEvent -= value;
            }
        }

        public MstProfilesClient(IClientSocket connection) : base(connection) { }

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
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected");
                return;
            }

            connection.SendMessage(MstOpCodes.ClientFillInProfileValues, profile.Count, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                // Use the bytes received, to replicate the profile
                profile.FromBytes(response.AsBytes());

                // Listen to profile updates, and apply them
                connection.RegisterMessageHandler(MstOpCodes.UpdateClientProfile, message =>
                {
                    profile.ApplyUpdates(message.AsBytes());
                });

                Current = profile;

                callback.Invoke(true, null);
                profileLoadEvent?.Invoke(profile);
            });
        }
    }
}