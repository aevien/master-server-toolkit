using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class GroupsDatabaseAccessor : IGroupsDatabaseAccessor
    {
        private readonly ConnectionConfig configuration;

        public MstProperties CustomProperties { get; private set; }

        public Logging.Logger Logger { get; set; }

        public GroupsDatabaseAccessor(ConnectionConfig configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            using SqlSugarClient db = new(configuration);

            var tableTypes = new[]
            {
                typeof(GroupData),
                typeof(GroupMemberData),
                typeof(GroupInviteData)
            };

            foreach (var tableType in tableTypes)
            {
                var tableName = db.EntityMaintenance.GetTableName(tableType);

                if (!db.DbMaintenance.IsAnyTable(tableName, false))
                    db.CodeFirst.InitTables(tableType);

                if (!db.DbMaintenance.IsAnyTable(tableName, false))
                    throw new InvalidOperationException($"Required database table '{tableName}' was not created");
            }
        }

        public void Dispose() { }

        public async Task<MstGroupInfo> CreateGroupAsync(MstGroupInfo group, MstGroupMemberInfo owner, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (group == null)
                throw new ArgumentNullException(nameof(group));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            using SqlSugarClient db = new(configuration);

            var groupData = GroupData.FromInfo(group);
            var ownerData = GroupMemberData.FromInfo(owner);

            var transaction = await db.Ado.UseTranAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await db.Insertable(groupData).ExecuteCommandAsync();
                cancellationToken.ThrowIfCancellationRequested();
                await db.Insertable(ownerData).ExecuteCommandAsync();
                cancellationToken.ThrowIfCancellationRequested();
            });

            if (!transaction.IsSuccess)
                throw transaction.ErrorException;

            return group;
        }

        public async Task<MstGroupInfo> GetGroupByIdAsync(string groupId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(groupId))
                return null;

            using SqlSugarClient db = new(configuration);

            var group = await db.Queryable<GroupData>()
                .Where(g => g.Id == groupId)
                .FirstAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return group?.ToInfo();
        }

        public async Task<MstGroupInfo> GetGroupByNameAsync(string groupName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(groupName))
                return null;

            string normalizedName = GroupData.NormalizeKey(groupName);

            using SqlSugarClient db = new(configuration);

            var group = await db.Queryable<GroupData>()
                .Where(g => g.NormalizedName == normalizedName)
                .FirstAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return group?.ToInfo();
        }

        public async Task<MstGroupInfo> GetGroupByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(tag))
                return null;

            string normalizedTag = GroupData.NormalizeKey(tag);

            using SqlSugarClient db = new(configuration);

            var group = await db.Queryable<GroupData>()
                .Where(g => g.NormalizedTag == normalizedTag)
                .FirstAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return group?.ToInfo();
        }

        public async Task<MstGroupMemberInfo> GetMemberByAccountIdAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return null;

            using SqlSugarClient db = new(configuration);

            var member = await db.Queryable<GroupMemberData>()
                .Where(m => m.AccountId == accountId)
                .FirstAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return member?.ToInfo();
        }

        public async Task<MstGroupMemberInfo> GetMemberAsync(string groupId, string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(accountId))
                return null;

            using SqlSugarClient db = new(configuration);

            var member = await db.Queryable<GroupMemberData>()
                .Where(m => m.GroupId == groupId && m.AccountId == accountId)
                .FirstAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return member?.ToInfo();
        }

        public async Task<List<MstGroupMemberInfo>> GetMembersAsync(string groupId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(groupId))
                return new List<MstGroupMemberInfo>();

            using SqlSugarClient db = new(configuration);

            var members = await db.Queryable<GroupMemberData>()
                .Where(m => m.GroupId == groupId)
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return members.Select(m => m.ToInfo()).ToList();
        }

        public async Task<int> CountMembersAsync(string groupId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(groupId))
                return 0;

            using SqlSugarClient db = new(configuration);

            int count = await db.Queryable<GroupMemberData>()
                .Where(m => m.GroupId == groupId)
                .CountAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return count;
        }

        public async Task<MstGroupInviteInfo> CreateInviteAsync(MstGroupInviteInfo invite, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (invite == null)
                throw new ArgumentNullException(nameof(invite));

            using SqlSugarClient db = new(configuration);

            await db.Insertable(GroupInviteData.FromInfo(invite)).ExecuteCommandAsync();

            return invite;
        }

        public async Task<MstGroupInviteInfo> GetInviteByIdAsync(string inviteId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(inviteId))
                return null;

            using SqlSugarClient db = new(configuration);

            var invite = await db.Queryable<GroupInviteData>()
                .Where(i => i.Id == inviteId)
                .FirstAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return invite?.ToInfo();
        }

        public async Task<MstGroupInviteInfo> GetPendingInviteAsync(string groupId, string toAccountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(toAccountId))
                return null;

            using SqlSugarClient db = new(configuration);

            int pending = (int)MstGroupInviteStatus.Pending;
            DateTime now = DateTime.UtcNow;

            var invite = await db.Queryable<GroupInviteData>()
                .Where(i => i.GroupId == groupId && i.ToAccountId == toAccountId && i.Status == pending && i.ExpiresAtUtc > now)
                .FirstAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return invite?.ToInfo();
        }

        public async Task<List<MstGroupInviteInfo>> GetPendingInvitesForAccountAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return new List<MstGroupInviteInfo>();

            using SqlSugarClient db = new(configuration);

            int pending = (int)MstGroupInviteStatus.Pending;
            DateTime now = DateTime.UtcNow;

            var invites = await db.Queryable<GroupInviteData>()
                .Where(i => i.ToAccountId == accountId && i.Status == pending && i.ExpiresAtUtc > now)
                .ToListAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return invites.Select(i => i.ToInfo()).ToList();
        }

        public async Task AcceptInviteAsync(string inviteId, MstGroupMemberInfo member, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(inviteId))
                throw new ArgumentException("Invite id cannot be empty", nameof(inviteId));

            if (member == null)
                throw new ArgumentNullException(nameof(member));

            using SqlSugarClient db = new(configuration);

            var memberData = GroupMemberData.FromInfo(member);
            int accepted = (int)MstGroupInviteStatus.Accepted;
            DateTime updatedAt = DateTime.UtcNow;

            var transaction = await db.Ado.UseTranAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await db.Insertable(memberData).ExecuteCommandAsync();
                cancellationToken.ThrowIfCancellationRequested();

                var inviteData = await db.Queryable<GroupInviteData>()
                    .Where(i => i.Id == inviteId)
                    .FirstAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (inviteData == null)
                    throw new InvalidOperationException($"Group invite [{inviteId}] was not found");

                inviteData.Status = accepted;
                inviteData.UpdatedAtUtc = updatedAt;

                await db.Updateable(inviteData).ExecuteCommandAsync();
                cancellationToken.ThrowIfCancellationRequested();
            });

            if (!transaction.IsSuccess)
                throw transaction.ErrorException;
        }

        public async Task RemoveMemberAsync(string groupId, string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(accountId))
                return;

            using SqlSugarClient db = new(configuration);

            await db.Deleteable<GroupMemberData>()
                .Where(m => m.GroupId == groupId && m.AccountId == accountId)
                .ExecuteCommandAsync();
        }
    }
}
