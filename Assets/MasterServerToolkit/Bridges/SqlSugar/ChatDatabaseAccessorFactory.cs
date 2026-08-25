using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class ChatDatabaseAccessorFactory : SqlSugarDatabaseAccessorFactory
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
            try
            {
                chatAccessor = new ChatDatabaseAccessor(configuration)
                {
                    Logger = logger
                };

                Mst.Server.DbAccessors.AddAccessor(chatAccessor);
            }
            catch (Exception e)
            {
                chatAccessor?.Dispose();
                chatAccessor = null;
                logger.Error($"Failed to setup {GetType().Name}");
                logger.Error(e);
            }
#endif
        }
    }
}
