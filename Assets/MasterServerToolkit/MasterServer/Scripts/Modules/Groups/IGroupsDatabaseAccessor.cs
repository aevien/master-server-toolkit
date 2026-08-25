using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public interface IGroupsDatabaseAccessor : IDatabaseAccessor
    {
        Task<MstGroupInfo> CreateGroupAsync(MstGroupInfo group, MstGroupMemberInfo owner, CancellationToken cancellationToken = default);

        Task<MstGroupInfo> GetGroupByIdAsync(string groupId, CancellationToken cancellationToken = default);

        Task<MstGroupInfo> GetGroupByNameAsync(string groupName, CancellationToken cancellationToken = default);

        Task<MstGroupInfo> GetGroupByTagAsync(string tag, CancellationToken cancellationToken = default);

        Task<MstGroupMemberInfo> GetMemberByAccountIdAsync(string accountId, CancellationToken cancellationToken = default);

        Task<MstGroupMemberInfo> GetMemberAsync(string groupId, string accountId, CancellationToken cancellationToken = default);

        Task<List<MstGroupMemberInfo>> GetMembersAsync(string groupId, CancellationToken cancellationToken = default);

        Task<int> CountMembersAsync(string groupId, CancellationToken cancellationToken = default);

        Task<MstGroupInviteInfo> CreateInviteAsync(MstGroupInviteInfo invite, CancellationToken cancellationToken = default);

        Task<MstGroupInviteInfo> GetInviteByIdAsync(string inviteId, CancellationToken cancellationToken = default);

        Task<MstGroupInviteInfo> GetPendingInviteAsync(string groupId, string toAccountId, CancellationToken cancellationToken = default);

        Task<List<MstGroupInviteInfo>> GetPendingInvitesForAccountAsync(string accountId, CancellationToken cancellationToken = default);

        Task AcceptInviteAsync(string inviteId, MstGroupMemberInfo member, CancellationToken cancellationToken = default);

        Task RemoveMemberAsync(string groupId, string accountId, CancellationToken cancellationToken = default);
    }
}
