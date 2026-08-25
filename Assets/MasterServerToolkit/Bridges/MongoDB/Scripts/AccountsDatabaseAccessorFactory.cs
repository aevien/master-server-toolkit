using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AccountsDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        [Tooltip("MongoDB client factory that supplies the shared client connection and database name used by the accounts accessor.")]
        private MongoDbClientFactory mongoDbClientFactory;

        #endregion

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private AccountsDatabaseAccessor accountsAccessor;
#endif

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
                accountsAccessor = new AccountsDatabaseAccessor(mongoDbClientFactory.Client, mongoDbClientFactory.Database)
                {
                    Logger = logger
                };

                Mst.Server.DbAccessors.AddAccessor(accountsAccessor);
            }
            catch (System.Exception e)
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
