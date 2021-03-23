using UnityEngine;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MongoDB.Driver;
#endif
using MasterServerToolkit.Logging;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class MongoDBFactory : MonoBehaviour
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a mongodb module, which sets up database accessors for the game"
        };

        [Header("MongoDB Settings")]
        public string DefaultConnectionString = "mongodb://localhost";
        public string DatabaseName = "masterServerToolkit";

        #if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private MongoClient _client;
        #endif

        [Header("Accounts DB Settings"), SerializeField]
        private bool useAccountDb = true;

        [Header("Profiles DB Settings"), SerializeField]
        private bool useProfilesDb = false;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(DefaultConnectionString))
            {
                DefaultConnectionString = "mongodb://localhost";
            }

            if (string.IsNullOrEmpty(DatabaseName))
            {
                DatabaseName = "masterServerToolkit";
            }
        }

        protected virtual void Awake()
        {
            #if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                var connectionString = Mst.Args.IsProvided(Mst.Args.Names.DbConnectionString)
                    ? Mst.Args.DbConnectionString
                    : DefaultConnectionString;

                _client = new MongoClient(connectionString);

                if (useAccountDb)
                    Mst.Server.DbAccessors.SetAccessor<IAccountsDatabaseAccessor>(new AccountsDatabaseAccessor(_client, DatabaseName));
                
                if (useProfilesDb)
                   Mst.Server.DbAccessors.SetAccessor<IProfilesDatabaseAccessor>(new ProfilesDatabaseAccessor(_client, DatabaseName));
            }
            catch (System.Exception e)
            {
                Logs.Error("Failed to setup MongoDB");
                Logs.Error(e);
            }
            #endif
        }
    }
}