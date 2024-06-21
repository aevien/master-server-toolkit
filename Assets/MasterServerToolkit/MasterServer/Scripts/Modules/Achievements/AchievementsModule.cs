using MasterServerToolkit.Networking;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementsModule : BaseServerModule
    {
        #region INSPECTOR

        [SerializeField]
        private ObservableAchievementsPopulator achievementsPopulator;

        #endregion

        private float waitForProfile = 60f;
        private ProfilesModule profilesModule;

        protected override void Awake()
        {
            base.Awake();

            AddOptionalDependency<ProfilesModule>();
        }

        public override void Initialize(IServer server)
        {
            // Modules dependency setup
            profilesModule = server.GetModule<ProfilesModule>();

            if (profilesModule == null)
            {
                logger.Error($"{GetType().Name} should use {nameof(ProfilesModule)}, but {nameof(ProfilesModule)} was not found");
            }

            profilesModule.OnProfileCreated += ProfilesModule_OnProfileCreated;

            server.RegisterMessageHandler(MstOpCodes.ClientUpdateAchievementProgress, ClientUpdateAchievementProgressRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientCheckAchievementProgress, ClientCheckAchievementProgressRequestHandler);
        }

        private void OnDestroy()
        {
            if (profilesModule != null)
                profilesModule.OnProfileCreated -= ProfilesModule_OnProfileCreated;
        }

        private void ProfilesModule_OnProfileCreated(ObservableServerProfile userProfile)
        {
            var achievementsProperty = achievementsPopulator.Populate() as ObservableAchievements;
            achievementsProperty.OnSetEvent += (oldValue, newValue) =>
            {
                var currentPropertyOwner = userProfile;

                if (achievementsProperty.IsProgressMet(newValue.id))
                {
                    currentPropertyOwner.ClientPeer.SendMessage(MstOpCodes.ClientAchievementProgressIsMet, newValue.id);
                }
            };

            userProfile.Add(achievementsProperty);
        }

        public AchievementData GetInfoById(string id)
        {
            return achievementsPopulator.Achievements.Where(a => a.id == id).FirstOrDefault();
        }

        #region MESSAGES

        private void ClientUpdateAchievementProgressRequestHandler(IIncomingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null)
            {
                message.Respond(ResponseStatus.Unauthorized);
                return;
            }

            var data = message.AsPacket<UpdateAchievementProgressPacket>();
            var profileExtension = userExtension.Peer.GetExtension<ProfilePeerExtension>();
            var achievementInfo = GetInfoById(data.id);

            if (achievementInfo == null)
            {
                message.Respond(ResponseStatus.NotFound);
                return;
            }

            var achievementsProperty = profileExtension.Profile.Get<ObservableAchievements>(ProfilePropertyOpCodes.achievements);

            if (!achievementsProperty.IsProgressMet(data.id))
            {
                achievementsProperty.UpdateProgress(data.id, data.value, achievementInfo.value);
            }
        }

        private void ClientCheckAchievementProgressRequestHandler(IIncomingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null)
            {
                message.Respond(ResponseStatus.Unauthorized);
                return;
            }

            var data = message.AsPacket<UpdateAchievementProgressPacket>();
            var profileExtension = userExtension.Peer.GetExtension<ProfilePeerExtension>();
            var achievementInfo = GetInfoById(message.AsString());

            var achievementsProperty = profileExtension.Profile.Get<ObservableAchievements>(ProfilePropertyOpCodes.achievements);

            if (achievementsProperty.ContainsProgress(data.id))
            {
                message.Respond(achievementsProperty.IsProgressMet(data.id), ResponseStatus.Success);
            }
            else
            {
                message.Respond(ResponseStatus.NotFound);
            }
        }

        #endregion
    }
}