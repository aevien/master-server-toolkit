#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class FriendsDatabaseAccessor : IFriendsDatabaseAccessor, IDisposable
    {
        private ILiteCollection<PlayerFriendsInfoData> friends;
        private ILiteCollection<FriendshipRequestInfoData> outgoingFriendshipRequests;
        private ILiteCollection<FriendshipRequestInfoData> incomingFriendshipRequests;
        private readonly ILiteDatabase database;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();

        public FriendsDatabaseAccessor(string databaseName)
        {
            database = new LiteDatabase($"{databaseName}.db");
        }

        public void InitCollections()
        {
            friends = database.GetCollection<PlayerFriendsInfoData>("friends");
            friends.EnsureIndex(a => a.UserId, true);

            outgoingFriendshipRequests = database.GetCollection<FriendshipRequestInfoData>("outgoingFriendshipRequests");
            outgoingFriendshipRequests.EnsureIndex(a => a.UserId, true);

            incomingFriendshipRequests = database.GetCollection<FriendshipRequestInfoData>("incomingFriendshipRequests");
            incomingFriendshipRequests.EnsureIndex(a => a.UserId, true);
        }

        public async Task<IFriendsInfoData> RestoreIncomingFriendshipRequests(string userId)
        {
            var friendshipRequests = new FriendshipRequestInfoData();

            await Task.Run(() =>
            {
                friendshipRequests = incomingFriendshipRequests.FindOne(a => a.UserId == userId);
            });

            return friendshipRequests;
        }

        public async Task<IFriendsInfoData> RestoreOutgoingFriendshipRequests(string userId)
        {
            var friendshipRequests = new FriendshipRequestInfoData();

            await Task.Run(() =>
            {
                friendshipRequests = outgoingFriendshipRequests.FindOne(a => a.UserId == userId);
            });

            return friendshipRequests;
        }

        public async Task<IFriendsInfoData> RestoreFriends(string userId)
        {
            var friendsInfo = new PlayerFriendsInfoData();

            await Task.Run(() =>
            {
                friendsInfo = friends.FindOne(a => a.UserId == userId);
            });

            return friendsInfo;
        }

        public async Task UpdateIncomingFriendshipRequests(string userId, HashSet<string> friendIds)
        {
            await Task.Delay(1);
        }

        public async Task UpdateOutgoingFriendshipRequests(string userId, HashSet<string> friendIds)
        {
            await Task.Delay(1);
        }

        public void Dispose()
        {
            database?.Dispose();
        }
    }
}

#endif