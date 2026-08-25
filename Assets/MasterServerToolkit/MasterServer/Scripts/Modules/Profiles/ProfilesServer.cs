using MasterServerToolkit.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MasterServerToolkit.Logging;

namespace MasterServerToolkit.MasterServer
{
    public class ProfilesServer : MstBaseClient
    {
        private sealed class ProfileSaveRequest
        {
            private bool isCompleted;

            public ProfileSaveRequest(byte[] payload, SuccessCallback callback)
            {
                Payload = payload;
                Callback = callback;
            }

            public byte[] Payload { get; }
            public SuccessCallback Callback { get; }

            public void Complete(bool isSuccessful, string error = null)
            {
                if (isCompleted)
                    return;

                isCompleted = true;

                try
                {
                    Callback?.Invoke(isSuccessful, error);
                }
                catch (System.Exception exception)
                {
                    Logs.Error($"Profile save callback failed: {exception}",
                        LogChannels.Economy);
                }
            }
        }

        /// <summary>
        /// List of loaded profiles
        /// </summary>
        private Dictionary<string, ObservableServerProfile> profilesList;

        /// <summary>
        /// List of modified profiles
        /// </summary>
        private HashSet<ObservableServerProfile> modifiedProfilesList;

        /// <summary>
        /// Confirmed profile saves waiting for a response from master.
        /// </summary>
        private readonly Dictionary<ObservableServerProfile, Queue<ProfileSaveRequest>> profileSaveRequests =
            new Dictionary<ObservableServerProfile, Queue<ProfileSaveRequest>>();

        /// <summary>
        /// Update profile task
        /// </summary>
        private Coroutine sendUpdatesCoroutine;

        /// <summary>
        /// Time, after which game server will try sending profile 
        /// updates to master server
        /// </summary>
        public float ProfileUpdatesInterval { get; set; } = 1f;

        public ProfilesServer(IClientSocket connection)
            : base(connection)
        {
            ProfilesClient.RegisterErrorParsers();
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
        /// Sends the current profile changes immediately and completes after the master
        /// confirms that the resulting profile was saved to the database.
        /// </summary>
        /// <param name="profile">Registered game-server profile to save.</param>
        /// <param name="callback">Callback invoked exactly once with the save result.</param>
        public void SaveProfile(ObservableServerProfile profile, SuccessCallback callback)
        {
            if (profile == null)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.Invalid));
                return;
            }

            if (!profilesList.TryGetValue(profile.UserId, out ObservableServerProfile registeredProfile) ||
                !ReferenceEquals(registeredProfile, profile))
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotFound));
                return;
            }

            if (Connection == null || !Connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            ProfileSaveRequest request;

            try
            {
                byte[] updates = profile.GetUpdates();
                request = new ProfileSaveRequest(
                    CreateProfileUpdatesPayload(profile, updates), callback);
            }
            catch (System.Exception exception)
            {
                Logs.Error($"Profile save payload build failed. userId={profile.UserId}, error={exception}",
                    LogChannels.Economy);
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.Error));
                return;
            }

            if (!profileSaveRequests.TryGetValue(profile, out Queue<ProfileSaveRequest> requests))
            {
                requests = new Queue<ProfileSaveRequest>();
                profileSaveRequests.Add(profile, requests);
            }

            bool startRequest = requests.Count == 0;
            requests.Enqueue(request);

            profile.ClearUpdates();
            modifiedProfilesList.Remove(profile);

            if (startRequest)
                SendProfileSaveRequest(profile, request);
        }

        /// <summary>
        /// Sends a request to server, retrieves all profile values, and applies them to a provided
        /// profile
        /// </summary>
        public void FillProfileValues(ObservableServerProfile profile, SuccessCallback callback = null)
        {
            FillProfileValues(profile, callback, Connection);
        }

        /// <summary>
        /// Sends a request to server, retrieves all profile values, and applies them to a provided
        /// profile
        /// </summary>
        public void FillProfileValues(ObservableServerProfile profile, SuccessCallback callback, IClientSocket connection)
        {
            bool isCompleted = false;

            void Complete(bool isSuccessful, string error = null)
            {
                if (isCompleted)
                    return;

                isCompleted = true;
                callback?.Invoke(isSuccessful, error);
            }

            if (connection == null || !connection.IsConnected)
            {
                Complete(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            try
            {
                connection.SendMessage(MstOpCodes.ServerFillInProfileValues, profile.UserId, (status, response) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        Complete(false, ReadServerError(status, response,
                            $"profile load for user {profile.UserId}"));
                        return;
                    }

                    try
                    {
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
                            Logs.Info($"Profile modified on game server. userId={profile.UserId}, dirtyProperties={profile.HasDirtyProperties}, connectionIsConnected={connection?.IsConnected}",
                                LogChannels.Economy);
                            OnProfileModified(profile, connection);
                        };

                        // Register to dispose event
                        profile.OnDisposedEvent += OnProfileDisposed;
                    }
                    catch (System.Exception ex)
                    {
                        Logs.Error($"Game server profile response processing failed. userId={profile.UserId}, error={ex}",
                            LogChannels.Economy);
                        Complete(false, Mst.Errors.Parse(ResponseStatus.Invalid));
                        return;
                    }

                    Complete(true);
                });
            }
            catch (System.Exception ex)
            {
                if (!isCompleted)
                {
                    Logs.Error($"Game server profile request failed. userId={profile.UserId}, error={ex}",
                        LogChannels.Economy);
                }

                Complete(false, Mst.Errors.Parse(ResponseStatus.Error));
            }
        }

        private void OnProfileModified(ObservableServerProfile profile, IClientSocket connection)
        {
            if (!modifiedProfilesList.Contains(profile))
                modifiedProfilesList.Add(profile);

            Logs.Info($"Profile update queued on game server. userId={profile.UserId}, pendingProfiles={modifiedProfilesList.Count}, coroutineActive={sendUpdatesCoroutine != null}",
                LogChannels.Economy);

            if (sendUpdatesCoroutine != null)
                return;

            var timer = MstTimer.Instance;

            if (timer == null)
            {
                Logs.Error($"Profile update coroutine was not started. reason=mst_timer_not_found, userId={profile.UserId}, pendingProfiles={modifiedProfilesList.Count}",
                    LogChannels.Economy);
                return;
            }

            Logs.Info($"Starting profile update coroutine. userId={profile.UserId}, pendingProfiles={modifiedProfilesList.Count}, timerActive={timer.gameObject.activeInHierarchy}, timerEnabled={timer.enabled}",
                LogChannels.Economy);

            sendUpdatesCoroutine = timer.StartCoroutine(KeepSendingUpdates(connection));

            Logs.Info($"Profile update coroutine start result. userId={profile.UserId}, pendingProfiles={modifiedProfilesList.Count}, handleIsNull={sendUpdatesCoroutine == null}",
                LogChannels.Economy);
        }

        public void StopSendingUpdates()
        {
            if (sendUpdatesCoroutine == null)
                return;

            MstTimer.TryStopCoroutine(sendUpdatesCoroutine);
            sendUpdatesCoroutine = null;
        }

        public override void ClearConnection(bool clearHandlers = true)
        {
            StopSendingUpdates();
            FailProfileSaveRequests();
            base.ClearConnection(clearHandlers);
        }

        protected override void OnConnectionStatusChanged(ConnectionStatus status)
        {
            if (status == ConnectionStatus.Disconnected)
            {
                StopSendingUpdates();
                FailProfileSaveRequests();
            }
        }

        private void OnProfileDisposed(ObservableServerProfile profile)
        {
            profile.OnDisposedEvent -= OnProfileDisposed;
            modifiedProfilesList.Remove(profile);
            profilesList.Remove(profile.UserId);

            if (profileSaveRequests.TryGetValue(profile, out Queue<ProfileSaveRequest> requests))
            {
                profileSaveRequests.Remove(profile);

                while (requests.Count > 0)
                    requests.Dequeue().Complete(false, Mst.Errors.Parse(ResponseStatus.Cancelled));
            }
        }

        private void SendProfileSaveRequest(ObservableServerProfile profile,
            ProfileSaveRequest request)
        {
            try
            {
                Connection.SendMessage(MstOpCodes.ServerUpdateProfileValues,
                    request.Payload, (status, response) =>
                    {
                        if (status == ResponseStatus.Success)
                        {
                            CompleteProfileSaveRequest(profile, request, true);
                            return;
                        }

                        CompleteProfileSaveRequest(profile, request, false,
                            ReadServerError(status, response,
                                $"profile save for user {profile.UserId}"));
                    });
            }
            catch (System.Exception exception)
            {
                Logs.Error($"Profile save request failed. userId={profile.UserId}, error={exception}",
                    LogChannels.Economy);
                CompleteProfileSaveRequest(profile, request, false,
                    Mst.Errors.Parse(ResponseStatus.Error));
            }
        }

        private string ReadServerError(ResponseStatus status, IIncomingMessage response,
            string operation)
        {
            if (response == null || !response.HasData)
            {
                Logs.Error($"Master rejected {operation}. status={status}, code=<none>",
                    LogChannels.Economy);
                return Mst.Errors.Parse(status);
            }

            try
            {
                MstProperties properties = MstProperties.FromBytes(response.AsBytes());
                string code = properties.AsString(MstErrorPropertyKeys.CODE);
                Logs.Error($"Master rejected {operation}. status={status}, code={code ?? "<none>"}",
                    LogChannels.Economy);
                return Mst.Errors.Parse(status, properties);
            }
            catch (System.Exception exception)
            {
                Logs.Error($"Game server failed to decode structured error for {operation}. error={exception}",
                    LogChannels.Economy);
                return Mst.Errors.Parse(status);
            }
        }

        private void CompleteProfileSaveRequest(ObservableServerProfile profile,
            ProfileSaveRequest request, bool isSuccessful, string error = null)
        {
            if (!profileSaveRequests.TryGetValue(profile, out Queue<ProfileSaveRequest> requests) ||
                requests.Count == 0 || !ReferenceEquals(requests.Peek(), request))
            {
                request.Complete(isSuccessful, error);
                return;
            }

            requests.Dequeue();
            ProfileSaveRequest nextRequest = null;
            var failedRequests = new List<ProfileSaveRequest>();

            if (!isSuccessful)
            {
                while (requests.Count > 0)
                    failedRequests.Add(requests.Dequeue());
            }
            else if (requests.Count > 0)
            {
                nextRequest = requests.Peek();
            }

            if (requests.Count == 0)
            {
                profileSaveRequests.Remove(profile);

                if (profile.HasDirtyProperties)
                    OnProfileModified(profile, Connection);
            }

            request.Complete(isSuccessful, error);

            foreach (ProfileSaveRequest failedRequest in failedRequests)
            {
                failedRequest.Complete(false,
                    Mst.Errors.Parse(ResponseStatus.DependencyError));
            }

            if (nextRequest != null &&
                profileSaveRequests.TryGetValue(profile,
                    out Queue<ProfileSaveRequest> currentRequests) &&
                currentRequests.Count > 0 &&
                ReferenceEquals(currentRequests.Peek(), nextRequest))
            {
                SendProfileSaveRequest(profile, nextRequest);
            }
        }

        private void FailProfileSaveRequests()
        {
            var requestsToFail = new List<ProfileSaveRequest>();

            foreach (Queue<ProfileSaveRequest> requests in profileSaveRequests.Values)
            {
                while (requests.Count > 0)
                    requestsToFail.Add(requests.Dequeue());
            }

            profileSaveRequests.Clear();

            foreach (ProfileSaveRequest request in requestsToFail)
                request.Complete(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
        }

        private static byte[] CreateProfileUpdatesPayload(
            ObservableServerProfile profile, byte[] updates)
        {
            using (var stream = new MemoryStream())
            using (var writer = new EndianBinaryWriter(
                EndianBitConverter.Big, stream))
            {
                writer.WriteCount32(1,
                    MstNetworkLimits.MaxProfilePropertyCount,
                    "Profile update batch");
                writer.Write(profile.UserId);
                writer.Write(updates.Length);
                writer.Write(updates);
                return stream.ToArray();
            }
        }

        private IEnumerator KeepSendingUpdates(IClientSocket connection)
        {
            Logs.Info($"Profile update coroutine started. pendingProfiles={modifiedProfilesList.Count}, connectionIsConnected={connection?.IsConnected}",
                LogChannels.Economy);

            while (true)
            {
                yield return new WaitForSeconds(ProfileUpdatesInterval);

                Logs.Info($"Profile update coroutine tick. pendingProfiles={modifiedProfilesList.Count}, connectionIsConnected={connection?.IsConnected}",
                    LogChannels.Economy);

                if (connection == null || !connection.IsConnected)
                {
                    Logs.Warn($"Profile update sending stopped. reason=connection_not_ready, pendingProfiles={modifiedProfilesList.Count}",
                        LogChannels.Economy);
                    sendUpdatesCoroutine = null;
                    yield break;
                }

                if (modifiedProfilesList.Count == 0)
                {
                    sendUpdatesCoroutine = null;
                    yield break;
                }

                var profilesToSend = new List<ObservableServerProfile>();

                foreach (ObservableServerProfile profile in modifiedProfilesList)
                {
                    if (!profileSaveRequests.ContainsKey(profile))
                        profilesToSend.Add(profile);
                }

                if (profilesToSend.Count == 0)
                {
                    sendUpdatesCoroutine = null;
                    yield break;
                }

                using (var ms = new MemoryStream())
                {
                    using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                    {
                        try
                        {
                            // Write profiles count
                            writer.WriteCount32(
                                profilesToSend.Count,
                                MstNetworkLimits.MaxProfilePropertyCount,
                                "Profile update batch");

                            foreach (var profile in profilesToSend)
                            {
                                // Write userId
                                writer.Write(profile.UserId);

                                var updates = profile.GetUpdates();

                                Logs.Info($"Profile update prepared on game server. userId={profile.UserId}, updateBytes={updates.Length}",
                                    LogChannels.Economy);

                                // Write updates length
                                writer.Write(updates.Length);

                                // Write updates
                                writer.Write(updates);

                            }
                        }
                        catch (System.Exception ex)
                        {
                            Logs.Error($"Profile update payload build failed. pendingProfiles={profilesToSend.Count}, error={ex}",
                                LogChannels.Economy);
                            sendUpdatesCoroutine = null;
                            yield break;
                        }

                        Logs.Info($"Profile update payload ready on game server. profiles={profilesToSend.Count}, payloadBytes={ms.Length}",
                            LogChannels.Economy);

                        try
                        {
                            connection.SendMessage(MstOpCodes.ServerUpdateProfileValues, ms.ToArray());

                            foreach (var profile in profilesToSend)
                                profile.ClearUpdates();

                            Logs.Info($"Profile updates sent to master. profiles={profilesToSend.Count}, payloadBytes={ms.Length}",
                                LogChannels.Economy);
                        }
                        catch (System.Exception ex)
                        {
                            Logs.Error($"Profile update send failed. profiles={profilesToSend.Count}, payloadBytes={ms.Length}, error={ex}",
                                LogChannels.Economy);
                            sendUpdatesCoroutine = null;
                            yield break;
                        }
                    }
                }

                foreach (ObservableServerProfile profile in profilesToSend)
                    modifiedProfilesList.Remove(profile);
            }
        }
    }
}
