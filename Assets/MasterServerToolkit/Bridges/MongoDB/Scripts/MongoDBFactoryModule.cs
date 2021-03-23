using UnityEngine;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MongoDB.Driver;
#endif

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class MongoDBFactoryModule : BaseServerModule
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up database accessors for the game"
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

        public override void Initialize(IServer server)
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
                logger.Error("Failed to setup MongoDB");
                logger.Error(e);
            }
            #endif
        }
    }
}