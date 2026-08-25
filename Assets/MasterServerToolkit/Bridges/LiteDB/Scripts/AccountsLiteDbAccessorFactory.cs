using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AccountsLiteDbAccessorFactory : LiteDatabaseAccessorFactory
    {
        private AccountsDatabaseAccessor accountsAccessor;

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            accountsAccessor?.Dispose();
            accountsAccessor = null;
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                accountsAccessor = new AccountsDatabaseAccessor(databaseName);
                accountsAccessor.Logger = logger;

                Mst.Server.DbAccessors.AddAccessor(accountsAccessor);
            }
            catch (Exception e)
            {
                accountsAccessor?.Dispose();
                accountsAccessor = null;
                logger.Error($"Failed to setup {nameof(AccountsDatabaseAccessor)}");
                logger.Error(e);
            }
#endif
        }
    }
}
