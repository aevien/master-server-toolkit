using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.MySQL
{
    public class ProfilesMySQLAccessorFactory : MySqlDatabaseAccessorFactory
    {
        private ProfilesDatabaseAccessor profilesAccessor;

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            profilesAccessor?.Dispose();
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                profilesAccessor = new ProfilesDatabaseAccessor(connectionString);
                Mst.Server.DbAccessors.AddAccessor(profilesAccessor);
            }
            catch (Exception e)
            {
                Logging.Logs.Error("Failed to setup LiteDB");
                Logging.Logs.Error(e);
            }
#endif
        }
    }
}