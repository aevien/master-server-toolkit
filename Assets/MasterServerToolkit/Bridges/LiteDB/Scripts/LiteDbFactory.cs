using System;
using MasterServerToolkit.MasterServer;
using UnityEngine;
using MasterServerToolkit.Logging;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using LiteDB;
#endif

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class LiteDbFactory : MonoBehaviour
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

        protected virtual void Awake()
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
                Logs.Error("Failed to setup LiteDB");
                Logs.Error(e);
            }
#endif
        }

        protected virtual void OnValidate()
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
    }
}