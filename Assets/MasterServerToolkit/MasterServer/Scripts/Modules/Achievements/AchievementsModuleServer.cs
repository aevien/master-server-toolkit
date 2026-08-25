using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementsModuleServer : MstBaseClient
    {
        private readonly List<UpdateAchievementProgressPacket> achievementsToUpdate = new();
        private Coroutine sendUpdatesCoroutine;
        private bool isUpdateRequestPending;

        public float UpdatesInterval { get; set; } = 1f;

        public AchievementsModuleServer(IClientSocket connection)
            : base(connection)
        {
            AchievementsModuleClient.RegisterErrorParsers();
            RegisterMessageHandler(MstOpCodes.ServerUpdateAchievementProgress, ServerUpdateAchievementProgress);
        }

        private void ServerUpdateAchievementProgress(IIncomingMessage message)
        {
            try
            {
                var data = message.AsPacket<UpdateAchievementProgressPacket>();

                if (!TryAddProgress(data.key, data.progress, data.userId,
                        out bool unlockedNow, out string errorCode, out string error))
                {
                    Logs.Warn(error);

                    if (message.IsExpectingResponse)
                    {
                        var properties = new MstProperties();
                        properties.Set(MstErrorPropertyKeys.USER_ID, data.userId);
                        properties.Set(MstErrorPropertyKeys.ACHIEVEMENT_KEY, data.key);
                        message.RespondError(ResponseStatus.NotFound, errorCode, properties);
                    }

                    return;
                }

                if (message.IsExpectingResponse)
                    message.Respond(unlockedNow, ResponseStatus.Success);
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to process achievement progress update: {exception}");

                if (message.IsExpectingResponse)
                    message.RespondError(ResponseStatus.Error,
                        MstErrorCodes.ACHIEVEMENT_UPDATE_FAILED);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="userId"></param>
        public void AddProgress(string key, string userId)
        {
            AddProgress(key, 1, userId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="progress"></param>
        /// <param name="userId"></param>
        public void AddProgress(string key, int progress, string userId)
        {
            if (!TryAddProgress(key, progress, userId, out _, out _, out string error))
                Logs.Error(error);
        }

        private bool TryAddProgress(string key, int progress, string userId, out bool unlockedNow,
            out string errorCode, out string error)
        {
            unlockedNow = false;
            errorCode = string.Empty;
            error = string.Empty;

            if (!TryGetProfile(userId, out var profile))
            {
                errorCode = MstErrorCodes.USER_PROFILE_NOT_FOUND;
                error = $"No profile found for user {userId}";
                return false;
            }

            if (!profile.TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements property))
            {
                errorCode = MstErrorCodes.USER_ACHIEVEMENTS_NOT_FOUND;
                error = $"Profile property {nameof(ProfilePropertyOpCodes.achievements)} not found for user {userId}";
                return false;
            }

            if (!property.Has(key))
            {
                errorCode = MstErrorCodes.ACHIEVEMENT_NOT_FOUND;
                error = $"Achievement {key} not found for user {userId}";
                return false;
            }

            unlockedNow = property.TryToUnlock(key, progress);

            if (!unlockedNow)
                return true;

            AchievementProgressInfo achievement = property.Get(key);
            var queuedUpdate = achievementsToUpdate.FirstOrDefault(d => d.key == key && d.userId == userId);

            if (queuedUpdate == null)
            {
                queuedUpdate = new UpdateAchievementProgressPacket()
                {
                    key = key,
                    userId = userId,
                    progress = achievement.progress
                };

                achievementsToUpdate.Add(queuedUpdate);
            }
            else
            {
                queuedUpdate.progress = achievement.progress;
            }

            if (sendUpdatesCoroutine == null)
                StartSendingUpdates();

            return true;
        }

        public void StopSendingUpdates()
        {
            isUpdateRequestPending = false;

            if (sendUpdatesCoroutine == null)
                return;

            MstTimer.TryStopCoroutine(sendUpdatesCoroutine);
            sendUpdatesCoroutine = null;
        }

        public override void ClearConnection(bool clearHandlers = true)
        {
            StopSendingUpdates();
            base.ClearConnection(clearHandlers);
        }

        protected override void OnConnectionStatusChanged(ConnectionStatus status)
        {
            if (status == ConnectionStatus.Disconnected)
            {
                StopSendingUpdates();
                return;
            }

            if (status == ConnectionStatus.Connected &&
                achievementsToUpdate.Count > 0 &&
                sendUpdatesCoroutine == null)
            {
                StartSendingUpdates();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="userId"></param>
        public void ResetProgress(string key, string userId)
        {
            if (!TryGetProfile(userId, out var profile))
            {
                Logs.Error($"No profile found for user {userId}");
                return;
            }

            if (!profile.TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements property))
            {
                Logs.Error($"Profile property {nameof(ProfilePropertyOpCodes.achievements)} not found for user {userId}");
                return;
            }

            property.Reset(key);
        }

        private IEnumerator KeepSendingUpdates()
        {
            while (sendUpdatesCoroutine != null)
            {
                yield return new WaitForSeconds(UpdatesInterval);

                if (!IsConnected)
                {
                    sendUpdatesCoroutine = null;
                    yield break;
                }

                if (achievementsToUpdate.Count == 0)
                {
                    sendUpdatesCoroutine = null;
                    yield break;
                }

                SendPendingUpdates();
            }
        }

        protected virtual void StartSendingUpdates()
        {
            MstTimer timer = MstTimer.Instance;

            if (timer != null)
                sendUpdatesCoroutine = timer.StartCoroutine(KeepSendingUpdates());
        }

        protected void SendPendingUpdates()
        {
            if (!IsConnected || isUpdateRequestPending || achievementsToUpdate.Count == 0)
                return;

            List<UpdateAchievementProgressPacket> pendingUpdates = achievementsToUpdate
                .Select(update => new UpdateAchievementProgressPacket
                {
                    key = update.key,
                    userId = update.userId,
                    progress = update.progress
                })
                .ToList();

            isUpdateRequestPending = true;

            try
            {
                Connection.SendMessage(MstOpCodes.ServerUpdateAchievementProgress,
                    pendingUpdates.ToBytes(), (status, response) =>
                    {
                        isUpdateRequestPending = false;

                        if (status == ResponseStatus.Success)
                        {
                            RemoveAcknowledgedUpdates(pendingUpdates);
                            return;
                        }

                        Logs.Error($"Master rejected achievement update batch. " +
                            $"status={status}, error={ReadServerError(status, response)}");
                    });
            }
            catch (Exception exception)
            {
                isUpdateRequestPending = false;
                Logs.Error($"Failed to send achievement updates: {exception}");
            }
        }

        private static string ReadServerError(ResponseStatus status, IIncomingMessage response)
        {
            if (response == null || !response.HasData)
                return status.ToString();

            try
            {
                MstProperties properties = MstProperties.FromBytes(response.AsBytes());
                string code = properties.AsString(MstErrorPropertyKeys.CODE);
                return string.IsNullOrWhiteSpace(code) ? status.ToString() : code;
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to decode structured achievement error from master: {exception}");
                return status.ToString();
            }
        }

        protected virtual bool TryGetProfile(string userId, out ObservableServerProfile profile)
        {
            return Mst.Server.Profiles.TryGetById(userId, out profile);
        }

        private void RemoveAcknowledgedUpdates(
            IEnumerable<UpdateAchievementProgressPacket> acknowledgedUpdates)
        {
            foreach (UpdateAchievementProgressPacket acknowledged in acknowledgedUpdates)
            {
                achievementsToUpdate.RemoveAll(queued =>
                    queued.key == acknowledged.key &&
                    queued.userId == acknowledged.userId &&
                    queued.progress <= acknowledged.progress);
            }
        }
    }
}
