using System;
using MasterServerToolkit.MasterServer;
using UnityEngine;
using MasterServerToolkit.Utils;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using LiteDB;
#endif

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class LiteDbFactoryModule : BaseServerModule
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up database accessors for the game"
        };

        [Header("Accounts DB Settings"), SerializeField]
        private bool useAccountDb = true;
        [SerializeField]
        private string accountDbName = "accounts";

        [Header("Profiles DB Settings"), SerializeField]
        private bool useProfilesDb = false;
        [SerializeField]
        private string profilesDbName = "profiles";

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(accountDbName))
            {
                accountDbName = "accounts";
            }

            if (string.IsNullOrEmpty(profilesDbName))
            {
                profilesDbName = "profiles";
            }
        }

        public override void Initialize(IServer server)
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                if (useAccountDb)
                    Mst.Server.DbAccessors.SetAccessor<IAccountsDatabaseAccessor>(new AccountsDatabaseAccessor(new LiteDatabase($"{accountDbName}.db")));
                
                if (useProfilesDb)
                    Mst.Server.DbAccessors.SetAccessor<IProfilesDatabaseAccessor>(new ProfilesDatabaseAccessor(new LiteDatabase($"{profilesDbName}.db")));
            }
            catch (Exception e)
            {
                logger.Error("Failed to setup LiteDB");
                logger.Error(e);
            }
#endif
        }
    }
}