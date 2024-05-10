using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class MongoDbClientFactory : MonoBehaviour
    {
        #region INSPECTOR

        [Header("MongoDB Settings"), SerializeField]
        private string defaultConnectionString = "mongodb://localhost";
        [SerializeField]
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

            // TODO
            // Client must be disconnected manually after this object will be destroyed
        }
    }
}
