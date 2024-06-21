using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class ProfilesDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private MongoDbClientFactory mongoDbClientFactory;
        [SerializeField] 
        private bool saveDataAsBytes;

        #endregion

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                if (saveDataAsBytes)
                    Mst.Server.DbAccessors.AddAccessor(new ProfilesDatabaseAccessor(mongoDbClientFactory.Client, mongoDbClientFactory.Database));
                else
                    Mst.Server.DbAccessors.AddAccessor(new ProfilesDocumentDatabaseAccessor(mongoDbClientFactory.Client, mongoDbClientFactory.Database));
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