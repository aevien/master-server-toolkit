using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class MongoDbClientFactory : MonoBehaviour
    {
        #region INSPECTOR

        [Header("MongoDB Settings"), SerializeField]
        [Tooltip("Fallback MongoDB connection URI used when -mstDatabaseConfiguration is not supplied. It must be a valid MongoDB driver URI; credentials may be provided through deployment configuration.")]
        private string defaultConnectionString = "mongodb://localhost";
        [SerializeField]
        [Tooltip("MongoDB database selected by all accessor factories that reference this client factory. The name is required and defaults to masterServerToolkit when empty.")]
        private string databaseName = "masterServerToolkit";

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public MongoClient Client { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public string Database => databaseName;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(defaultConnectionString))
            {
                defaultConnectionString = "mongodb://localhost";
            }

            if (string.IsNullOrEmpty(databaseName))
            {
                databaseName = "masterServerToolkit";
            }
        }

        private void Awake()
        {
            ConnectionString = Mst.Args.AsString(Mst.Args.Names.DatabaseConfiguration, defaultConnectionString);
            Client = new MongoClient(ConnectionString);
        }
    }
}
