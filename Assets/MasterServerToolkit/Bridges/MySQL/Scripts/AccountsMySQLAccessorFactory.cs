using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.MySQL
{
    public class AccountsMySQLAccessorFactory : MySqlDatabaseAccessorFactory
    {
        private AccountsDatabaseAccessor accountsAccessor;

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            accountsAccessor?.Dispose();
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                accountsAccessor = new AccountsDatabaseAccessor(connectionString);
                Mst.Server.DbAccessors.AddAccessor(accountsAccessor);
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