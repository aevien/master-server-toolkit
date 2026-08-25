using MasterServerToolkit.Networking;
using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Reports the result of an achievement progress request.
    /// </summary>
    /// <param name="isSuccessful">Whether the request completed successfully.</param>
    /// <param name="unlockedNow">Whether this request unlocked the achievement.</param>
    /// <param name="error">Failure description, or an empty string on success.</param>
    public delegate void AchievementProgressCallback(bool isSuccessful, bool unlockedNow, string error);

    public class AchievementsModuleClient : MstBaseClient
    {
        public event Action<string> OnAchievementUnlocked;

        public IEnumerable<AchievementProgressInfo> GetProgresses()
        {
            if (!Mst.Client.Profiles.IsLoaded)
                return Enumerable.Empty<AchievementProgressInfo>();

            if (!Mst.Client.Profiles.Current.TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements property))
                return Enumerable.Empty<AchievementProgressInfo>();

            return property.Value;
        }

        public AchievementsModuleClient(IClientSocket connection) : base(connection)
        {
            RegisterErrorParsers();
            RegisterMessageHandler(MstOpCodes.ClientAchievementUnlocked, OnUnlocked);
        }

        internal static void RegisterErrorParsers()
        {
            Mst.Errors.TryRegister(MstErrorCodes.PERMISSION_DENIED);
            Mst.Errors.TryRegister(MstErrorCodes.USER_IS_NOT_LOGGED_IN);
            Mst.Errors.TryRegister(MstErrorCodes.USER_PROFILE_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.USER_ACHIEVEMENTS_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.ACHIEVEMENT_KEY_REQUIRED);
            Mst.Errors.TryRegister(MstErrorCodes.ACHIEVEMENT_UPDATE_FORBIDDEN);
            Mst.Errors.TryRegister(MstErrorCodes.ROOMS_MODULE_UNAVAILABLE);
            Mst.Errors.TryRegister(MstErrorCodes.JOINED_ROOM_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.ACHIEVEMENT_NOT_FOUND, properties =>
                Mst.Errors.LocalizeFormat("ui.error.achievements.not_found.message",
                    properties.AsString(MstErrorPropertyKeys.ACHIEVEMENT_KEY)));
            Mst.Errors.TryRegister(MstErrorCodes.ACHIEVEMENT_UPDATE_FAILED);
            Mst.Errors.TryRegister(MstErrorCodes.ACHIEVEMENT_RESPONSE_MISSING);
            Mst.Errors.TryRegister(MstErrorCodes.ACHIEVEMENT_RESPONSE_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACHIEVEMENT_RESPONSE_MISSING);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACHIEVEMENT_RESPONSE_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACHIEVEMENT_FORWARD_FAILED);
            Mst.Errors.TryRegister(MstErrorCodes.ACHIEVEMENT_UPDATES_NOT_APPLIED, properties =>
                Mst.Errors.LocalizeFormat("ui.error.achievements.updates_not_applied.message",
                    properties.AsInt(MstErrorPropertyKeys.COUNT)));
        }

        private void OnUnlocked(IIncomingMessage message)
        {
            OnAchievementUnlocked?.Invoke(message.AsString());
        }

        /// <summary>
        /// Updates achievement progress through the current connection.
        /// </summary>
        /// <param name="key">Achievement key.</param>
        /// <param name="progress">Progress amount to add.</param>
        public void UpdateProgress(string key, int progress)
        {
            UpdateProgress(key, progress, null, Connection);
        }

        /// <summary>
        /// Updates achievement progress and reports whether this request unlocked it.
        /// </summary>
        /// <param name="key">Achievement key.</param>
        /// <param name="progress">Progress amount to add.</param>
        /// <param name="callback">Operation result callback.</param>
        public void UpdateProgressWithResult(string key, int progress,
            AchievementProgressCallback callback)
        {
            UpdateProgress(key, progress, callback, Connection);
        }

        /// <summary>
        /// Updates achievement progress through the specified connection.
        /// </summary>
        /// <param name="key">Achievement key.</param>
        /// <param name="progress">Progress amount to add.</param>
        /// <param name="connection">Connection used for the request.</param>
        public void UpdateProgress(string key, int progress, IClientSocket connection)
        {
            UpdateProgress(key, progress, null, connection);
        }

        /// <summary>
        /// Updates achievement progress through the specified connection.
        /// </summary>
        /// <param name="key">Achievement key.</param>
        /// <param name="progress">Progress amount to add.</param>
        /// <param name="callback">Operation result callback.</param>
        /// <param name="connection">Connection used for the request.</param>
        public void UpdateProgress(string key, int progress, AchievementProgressCallback callback,
            IClientSocket connection)
        {
            bool isCompleted = false;

            void Complete(bool isSuccessful, bool unlockedNow, string error)
            {
                if (isCompleted)
                    return;

                isCompleted = true;
                callback?.Invoke(isSuccessful, unlockedNow, error);
            }

            if (connection == null)
            {
                Complete(false, false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (!connection.IsConnected)
            {
                Complete(false, false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                Complete(false, false, Mst.Errors.Parse(ResponseStatus.Invalid,
                    CreateErrorProperties(MstErrorCodes.ACHIEVEMENT_KEY_REQUIRED)));
                return;
            }

            var data = new UpdateAchievementProgressPacket()
            {
                key = key,
                progress = progress,
                userId = string.Empty
            };

            try
            {
                connection.SendMessage(MstOpCodes.ClientUpdateAchievementProgress, data, (status, response) =>
                {
                    if (status != ResponseStatus.Success)
                    {
                        Complete(false, false, Mst.Errors.Parse(status, response));
                        return;
                    }

                    if (response == null || !response.HasData)
                    {
                        Complete(false, false, Mst.Errors.Parse(ResponseStatus.DependencyError,
                            CreateErrorProperties(MstErrorCodes.ACHIEVEMENT_RESPONSE_MISSING)));
                        return;
                    }

                    bool unlockedNow;

                    try
                    {
                        unlockedNow = response.AsBool();
                    }
                    catch (Exception exception)
                    {
                        Logs.Error($"Achievement progress response is invalid: {exception}");
                        Complete(false, false, Mst.Errors.Parse(ResponseStatus.DependencyError,
                            CreateErrorProperties(MstErrorCodes.ACHIEVEMENT_RESPONSE_INVALID)));
                        return;
                    }

                    Complete(true, unlockedNow, string.Empty);
                });
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to send achievement progress: {exception}");
                Complete(false, false, Mst.Errors.Parse(
                    connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected));
            }
        }

        private static MstProperties CreateErrorProperties(string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);
            return properties;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsUnlocked(string key)
        {
            var progress = GetProgresses().FirstOrDefault(p => p.key == key);

            if (progress == null)
            {
                return false;
            }
            else
            {
                return progress.IsUnlocked;
            }
        }
    }
}
