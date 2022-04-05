using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class MstProfilesClient : MstBaseClient
    {
        public event Action<ObservableProfile> OnProfileLoadedEvent;

        public MstProfilesClient(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// Sends a request to server, retrieves all profile values, and applies them to a provided
        /// profile
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="callback"></param>
        public void GetProfileValues(ObservableProfile profile, SuccessCallback callback)
        {
            GetProfileValues(profile, callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, retrieves all profile values, and applies them to a provided profile
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void GetProfileValues(ObservableProfile profile, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected");
                return;
            }

            connection.SendMessage((ushort)MstOpCodes.ClientProfileRequest, profile.PropertyCount, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                // Use the bytes received, to replicate the profile
                profile.FromBytes(response.AsBytes());

                //
                OnProfileLoadedEvent?.Invoke(profile);

                // Listen to profile updates, and apply them
                connection.RegisterMessageHandler((ushort)MstOpCodes.UpdateClientProfile, message =>
                {
                    profile.ApplyUpdates(message.AsBytes());
                });

                callback.Invoke(true, null);
            });
        }
    }
}