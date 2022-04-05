#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class FriendshipRequestInfoData : IFriendsInfoData
    {
        [BsonId]
        public string UserId { get; set; }
        public string[] UsersIds { get; set; }
        public DateTime LastUpdate { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }
}

#endif