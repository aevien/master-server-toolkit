using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class MstProfilesClient : MstBaseClient
    {
        /// <summary>
        /// Currently loaded user profile
        /// </summary>
        public ObservableProfile Current { get; private set; }

        /// <summary>
        /// Checks if profile exists
        /// </summary>
        public bool HasProfile => Current != null;

        public event Action<ObservableProfile> OnProfileLoadedEvent;

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
                OnProfileLoadedEvent?.Invoke(profile);
            });
        }
    }
}