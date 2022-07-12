using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AccountsLiteDbAccessorFactory : DatabaseAccessorFactory
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a factory, which sets up database accessors for the game"
        };

        [Header("Accounts DB Settings"), SerializeField]
        private string accountDbName = "accounts";

        private AccountsDatabaseAccessor accountsAccessor;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(accountDbName))
            {
                accountDbName = "accounts";
            }
        }

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            accountsAccessor?.Dispose();
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                accountsAccessor = new AccountsDatabaseAccessor(accountDbName);
                Mst.Server.DbAccessors.AddAccessor(accountsAccessor);
                accountsAccessor.InitCollections();
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