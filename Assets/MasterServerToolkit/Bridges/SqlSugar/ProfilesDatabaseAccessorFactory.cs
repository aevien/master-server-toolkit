using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class ProfilesDatabaseAccessorFactory : SqlSugarDatabaseAccessorFactory
    {
        private ProfilesDatabaseAccessor accessor;

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            accessor?.Dispose();
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                accessor = new ProfilesDatabaseAccessor(configuration)
                {
                    Logger = logger
                };

                Mst.Server.DbAccessors.AddAccessor(accessor);
            }
            catch (Exception e)
            {
                logger.Error($"Failed to setup {GetType().Name}");
                logger.Error(e);
            }
#endif
        }
    }
}