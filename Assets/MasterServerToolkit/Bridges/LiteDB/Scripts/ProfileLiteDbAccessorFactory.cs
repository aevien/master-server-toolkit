using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using UnityEngine;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
#endif

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ProfileLiteDbAccessorFactory : DatabaseAccessorFactory
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up database accessors for the game"
        };

        [Header("Profiles DB Settings"), SerializeField]
        private bool useProfilesDb = false;
        [SerializeField]
        private string profilesDbName = "profiles";
        private ProfilesDatabaseAccessor profilesAccessor;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(profilesDbName))
            {
                profilesDbName = "profiles";
            }
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                if (useProfilesDb)
                {
                    profilesAccessor = new ProfilesDatabaseAccessor(profilesDbName);
                    Mst.Server.DbAccessors.SetAccessor<IProfilesDatabaseAccessor>(profilesAccessor);
                    profilesAccessor.InitCollections();
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