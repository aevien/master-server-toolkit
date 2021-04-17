using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using UnityEngine;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
#endif

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AccountsLiteDbAccessorFactory : DatabaseAccessorFactory
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up database accessors for the game"
        };

        [Header("Accounts DB Settings"), SerializeField]
        private bool useAccountDb = true;
        [SerializeField]
        private string accountDbName = "accounts";

        private AccountsDatabaseAccessor accountsAccessor;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(accountDbName))
            {
                accountDbName = "accounts";
            }
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                if (useAccountDb)
                {
                    accountsAccessor = new AccountsDatabaseAccessor(accountDbName);
                    Mst.Server.DbAccessors.SetAccessor<IAccountsDatabaseAccessor>(accountsAccessor);
                    accountsAccessor.InitCollections();
                }
            }
            catch (Exception e)
            {
                Logging.Logs.Error("Failed to setup LiteDB");
                Logging.Logs.Error(e);
            }
#endif
        }
    }
}