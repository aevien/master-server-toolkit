using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class ProfilesDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        [Tooltip("Required MongoDbClientFactory that provides the initialized client and database name used by the profiles accessor.")]
        private MongoDbClientFactory mongoDbClientFactory;
        [SerializeField]
        [Tooltip("Selects the profile storage format. Enable to store each complete profile as a binary payload; disable to store profile values as a MongoDB document.")]
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
