using MasterServerToolkit.Networking;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static MasterServerToolkit.MasterServer.AchievementData;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementsModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Permission"), SerializeField]
        protected bool clientCanUpdateProgress = false;

        [Header("Settings"), SerializeField]
        protected AchievementsDatabase achievementsDatabase;

        #endregion

        private AuthModule authModule;
        private ProfilesModule profilesModule;

        protected override void Awake()
        {
            base.Awake();

            AddDependency<AuthModule>();
            AddDependency<ProfilesModule>();
        }

        private void OnDestroy()
        {
            if (profilesModule != null)
                profilesModule.OnProfileLoaded += ProfilesModule_OnProfileLoaded;
        }

        public override void Initialize(IServer server)
        {
            // Modules dependency setup
            authModule = server.GetModule<AuthModule>();
            profilesModule = server.GetModule<ProfilesModule>();

            if (authModule == null)
            {
                logger.Error($"{GetType().Name} should use {nameof(AuthModule)}, but {nameof(AuthModule)} was not found");
            }

            if (profilesModule == null)
            {
                logger.Error($"{GetType().Name} should use {nameof(ProfilesModule)}, but {nameof(ProfilesModule)} was not found");
            }

            profilesModule.OnProfileLoaded += ProfilesModule_OnProfileLoaded;

            server.RegisterMessageHandler(MstOpCodes.ServerUpdateAchievementProgress, ServerUpdateAchievementProgressRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientUpdateAchievementProgress, ClientUpdateAchievementProgressRequestHandler);
        }

        private void ProfilesModule_OnProfileLoaded(ObservableServerProfile profile)
        {
            if (profile.TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements propery))
            {
                foreach (var achievement in achievementsDatabase.Achievements)
                {
                    if (!propery.Has(achievement.Key))
                    {
                        propery.Add(new AchievementProgressInfo(achievement));
                    }
                }
            }
            else
            {
                logger.Error("You're using the achievements module, but it looks like you haven't added the achievements property to the profile module's populators database.");
            }
        }

        private void InvokeAchievementResultCommand(IUserPeerExtension user, string key)
        {
            var achievementData = achievementsDatabase.Achievements.ToList().Find(a => a.Key == key);

            if (achievementData != null)
            {
                OnAchievementResultCommand(user, key, achievementData.ResultCommands);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <param name="key"></param>
        /// <param name="resultCommands"></param>
        /// <exception cref="NotImplementedException"></exception>
        protected virtual void OnAchievementResultCommand(IUserPeerExtension user, string key, AchievementExtraData[] resultCommands) { }

        #region MESSAGES

        private Task ClientUpdateAchievementProgressRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!clientCanUpdateProgress)
                {
                    message.Respond(ResponseStatus.Unauthorized);
                    return Task.CompletedTask;
                }

                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                if (userExtension == null)
                {
                    message.Respond(ResponseStatus.Unauthorized);
                    return Task.CompletedTask;
                }

                var data = message.AsPacket<UpdateAchievementProgressPacket>();

                if (userExtension.Peer.TryGetExtension(out ProfilePeerExtension profile)
                        && profile.Profile.TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements property)
                        && property.TryToUnlock(data.key, data.progress))
                {
                    InvokeAchievementResultCommand(userExtension, data.key);
                    message.Respond(ResponseStatus.Success);
                }
                else
                {
                    message.Respond(ResponseStatus.Default);
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        private Task ServerUpdateAchievementProgressRequestHandler(IIncomingMessage message)
        {
            try
            {
                var updateList = message.AsPacketsList<UpdateAchievementProgressPacket>();

                foreach (var data in updateList)
                {
                    var userExtension = authModule.GetLoggedInUserById(data.userId);

                    if (userExtension != null
                        && userExtension.Peer.TryGetExtension(out ProfilePeerExtension profile)
                        && profile.Profile.TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements property)
                        && property.TryToUnlock(data.key, data.progress))
                    {
                        userExtension.Peer.SendMessage(MstOpCodes.ClientAchievementUnlocked, data.key);
                        InvokeAchievementResultCommand(userExtension, data.key);
                    }
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        #endregion
    }
}