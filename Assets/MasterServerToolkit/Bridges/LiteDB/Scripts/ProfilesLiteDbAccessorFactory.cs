using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System;
using UnityEngine;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
#endif

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ProfilesLiteDbAccessorFactory : DatabaseAccessorFactory
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up database accessors for the game"
        };

        [Header("Profiles DB Settings"), SerializeField]
        private string profilesDbName = "profiles";

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private ProfilesDatabaseAccessor profilesAccessor;
#endif

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(profilesDbName))
            {
                profilesDbName = "profiles";
            }
        }

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            profilesAccessor?.Dispose();
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                profilesAccessor = new ProfilesDatabaseAccessor(profilesDbName);
                Mst.Server.DbAccessors.AddAccessor(profilesAccessor);
                profilesAccessor.InitCollections();
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