using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AccountsLiteDbAccessorFactory : LiteDatabaseAccessorFactory
    {
        private AccountsDatabaseAccessor accessor;

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
                accessor = new AccountsDatabaseAccessor(databaseName);
                accessor.Logger = logger;

                Mst.Server.DbAccessors.AddAccessor(accessor);
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