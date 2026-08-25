using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ChatLiteDbAccessorFactory : LiteDatabaseAccessorFactory
    {
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
            if (chatAccessor != null)
                return;

            try
            {
                chatAccessor = new ChatDatabaseAccessor(databaseName);
                chatAccessor.Logger = logger;

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
