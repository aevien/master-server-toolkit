using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MstProfilesServer : MstBaseClient
    {
        /// <summary>
        /// List of loaded profiles
        /// </summary>
        private Dictionary<string, ObservableServerProfile> profilesList;

        /// <summary>
        /// List of modified profiles
        /// </summary>
        private HashSet<ObservableServerProfile> modifiedProfilesList;

        /// <summary>
        /// Update profile task
        /// </summary>
        private Coroutine sendUpdatesCoroutine;

        /// <summary>
        /// Time, after which game server will try sending profile 
        /// updates to master server
        /// </summary>
        public float ProfileUpdatesInterval { get; set; } = 0.1f;

        public MstProfilesServer(IClientSocket connection) : base(connection)
        {
            profilesList = new Dictionary<string, ObservableServerProfile>();
            modifiedProfilesList = new HashSet<ObservableServerProfile>();
        }

        /// <summary>
        /// Gets user profile by user id
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public ObservableServerProfile GetById(string userId)
        {
            profilesList.TryGetValue(userId, out ObservableServerProfile profile);
            return profile;
        }

        /// <summary>
        /// Gets user profile by user id
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="profile"></param>
        /// <returns></returns>
        public bool TryGetById(string userId, out ObservableServerProfile profile)
        {
            profile = GetById(userId);
            return profile != null;
        }

        /// <summary>
        /// Sends a request to server, retrieves all profile values, and applies them to a provided
        /// profile
        /// </summary>
        public void FillProfileValues(ObservableServerProfile profile, SuccessCallback callback)
        {
            FillProfileValues(profile, callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, retrieves all profile values, and applies them to a provided
        /// profile
        /// </summary>
        public void FillProfileValues(ObservableServerProfile profile, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected");
                return;
            }

            connection.SendMessage(MstOpCodes.ServerFillInProfileValues, profile.UserId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                byte[] rawProfile = response.AsBytes();

                // Use the bytes received, to replicate the profile
                profile.FromBytes(rawProfile);

                // Clear all updates if exist
                profile.ClearUpdates();

                // Add profile to list
                profilesList[profile.UserId] = profile;

                // Register listener for modified 
                profile.OnModifiedInServerEvent += serverProfile =>
                {
                    OnProfileModified(profile, connection);
                };

                // Register to dispose event
                profile.OnDisposedEvent += OnProfileDisposed;

                callback.Invoke(true, null);
            });
        }

        private void OnProfileModified(ObservableServerProfile profile, IClientSocket connection)
        {
            if (!modifiedProfilesList.Contains(profile))
                modifiedProfilesList.Add(profile);

            if (sendUpdatesCoroutine != null)
            {
                return;
            }

            sendUpdatesCoroutine = SafeCoroutine.PermanentRunner.StartCoroutine(KeepSendingUpdates(connection));
        }

        private void OnProfileDisposed(ObservableServerProfile profile)
        {
            profile.OnDisposedEvent -= OnProfileDisposed;
            profilesList.Remove(profile.UserId);
        }

        private IEnumerator KeepSendingUpdates(IClientSocket connection)
        {
            while (true)
            {
                yield return new WaitForSeconds(ProfileUpdatesInterval);

                if (modifiedProfilesList.Count == 0)
                {
                    continue;
                }

                using (var ms = new MemoryStream())
                {
                    using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                    {
                        // Write profiles count
                        writer.Write(modifiedProfilesList.Count);

                        foreach (var profile in modifiedProfilesList)
                        {
                            // Write userId
                            writer.Write(profile.UserId);

                            var updates = profile.GetUpdates();

                            // Write updates length
                            writer.Write(updates.Length);

                            // Write updates
                            writer.Write(updates);

                            profile.ClearUpdates();
                        }

                        connection.SendMessage(MstOpCodes.ServerUpdateProfileValues, ms.ToArray());
                    }
                }

                modifiedProfilesList.Clear();
            }
        }
    }
}