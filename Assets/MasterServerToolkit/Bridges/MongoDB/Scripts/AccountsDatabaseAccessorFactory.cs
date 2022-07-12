using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AccountsDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        #region INSPECTOR

        public HelpBox _header = new HelpBox()
        {
            Text = "This is Accounts DB factory. Assign it to ProfilesModule"
        };

        [Header("Components"), SerializeField]
        private MongoDbClientFactory mongoDbClientFactory;

        #endregion

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                Mst.Server.DbAccessors.AddAccessor(new AccountsDatabaseAccessor(mongoDbClientFactory.Client, mongoDbClientFactory.Database));
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
