using MasterServerToolkit.Networking;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class QuestsModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Permission"), SerializeField]
        protected bool clientCanUpdateProgress = false;

        [Header("Settings"), SerializeField]
        protected QuestsDatabase[] questsDatabases;

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
        }

        private void ProfilesModule_OnProfileLoaded(ObservableServerProfile profile)
        {
            if (profile.TryGet(ProfilePropertyOpCodes.quests, out ObservableQuests propery))
            {
                
            }
            else
            {
                logger.Error("You're using the quests module, but it looks like you haven't added the quests property to the profile module's populators database.");
            }
        }

        #region MESSAGE HANDLERS

        private Task ClientGetQuestsMessageHandlers(IIncomingMessage message)
        {
            try
            {
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                if (userExtension == null)
                {
                    message.Respond(ResponseStatus.Unauthorized);
                    return Task.CompletedTask;
                }

                // Code here 

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        private Task ClientStartQuestMessageHandlers(IIncomingMessage message)
        {
            try
            {
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                if (userExtension == null)
                {
                    message.Respond(ResponseStatus.Unauthorized);
                    return Task.CompletedTask;
                }

                // Code here 

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        private Task ClientUpdateQuestProgressMessageHandlers(IIncomingMessage message)
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

                // Code here 

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        private Task ClientCancelQuestMessageHandlers(IIncomingMessage message)
        {
            try
            {
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                if (userExtension == null)
                {
                    message.Respond(ResponseStatus.Unauthorized);
                    return Task.CompletedTask;
                }

                // Code here 

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