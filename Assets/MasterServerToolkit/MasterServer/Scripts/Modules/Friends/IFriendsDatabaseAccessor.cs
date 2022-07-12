using System.Collections.Generic;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public interface IFriendsDatabaseAccessor:IDatabaseAccessor
    {
        Task<IFriendsInfoData> RestoreFriends(string userId);
        Task<IFriendsInfoData> RestoreIncomingFriendshipRequests(string userId);
        Task<IFriendsInfoData> RestoreOutgoingFriendshipRequests(string userId);
        Task UpdateIncomingFriendshipRequests(string userId, HashSet<string> friendIds);
        Task UpdateOutgoingFriendshipRequests(string userId, HashSet<string> friendIds);
    }
}