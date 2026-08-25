using MasterServerToolkit.MasterServer;
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class ChatDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        [Header("Components"), SerializeField]
        [Tooltip("MongoDB client factory that supplies the shared client connection and database name used by the chat accessor.")]
        private MongoDbClientFactory mongoDbClientFactory;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private ChatDatabaseAccessor chatAccessor;
#endif

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            chatAccessor?.Dispose();
            chatAccessor = null;
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                chatAccessor = new ChatDatabaseAccessor(mongoDbClientFactory.Client, mongoDbClientFactory.Database)
                {
                    Logger = logger
                };

                Mst.Server.DbAccessors.AddAccessor(chatAccessor);
            }
            catch (Exception e)
            {
                chatAccessor?.Dispose();
                chatAccessor = null;
                logger.Error($"Failed to setup {nameof(ChatDatabaseAccessor)}");
                logger.Error(e);
            }
#endif
        }
    }
}
