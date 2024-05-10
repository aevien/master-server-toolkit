using MasterServerToolkit.MasterServer;
using System;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
#endif

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ProfilesLiteDbAccessorFactory : LiteDatabaseAccessorFactory
    {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private ProfilesDatabaseAccessor profilesAccessor;
#endif

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
                profilesAccessor = new ProfilesDatabaseAccessor(databaseName);
                profilesAccessor.Logger = logger;

                Mst.Server.DbAccessors.AddAccessor(profilesAccessor);
            }
            catch (Exception e)
            {
                Logging.Logs.Error($"Failed to setup {nameof(ProfilesDatabaseAccessor)}");
                Logging.Logs.Error(e);
            }
#endif
        }
    }
}