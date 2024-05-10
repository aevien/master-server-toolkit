using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AccountsDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private MongoDbClientFactory mongoDbClientFactory;

        #endregion

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                var accessor = new AccountsDatabaseAccessor(mongoDbClientFactory.Client, mongoDbClientFactory.Database);
                accessor.Logger = logger;

                Mst.Server.DbAccessors.AddAccessor(accessor);
            }
            catch (System.Exception e)
            {
                logger.Error("Failed to setup MongoDB");
                logger.Error(e);
            }
#endif
        }
    }
}