using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System;
using UnityEngine;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
#endif

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class FriendsLiteDbAccessorFactory : DatabaseAccessorFactory
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script sets up friends database accessors for the game"
        };

        [Header("Friends DB Settings"), SerializeField]
        private string friendsDbName = "friends";

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private FriendsDatabaseAccessor friendsDatabaseAccessor;
#endif

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(friendsDbName))
            {
                friendsDbName = "friends";
            }
        }

        private void OnDestroy()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            friendsDatabaseAccessor?.Dispose();
#endif
        }

        public override void CreateAccessors()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                friendsDatabaseAccessor = new FriendsDatabaseAccessor(friendsDbName);
                Mst.Server.DbAccessors.AddAccessor(friendsDatabaseAccessor);
                friendsDatabaseAccessor.InitCollections();
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