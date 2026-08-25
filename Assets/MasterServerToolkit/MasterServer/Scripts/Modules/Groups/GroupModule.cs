using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class GroupModule : BaseServerModule
    {
        [Header("Persistence")]
        [SerializeField, Tooltip("Factory that creates and registers IGroupsDatabaseAccessor. Group creation, membership and invitations are unavailable when no groups accessor is registered.")]
        protected DatabaseAccessorFactory databaseAccessorFactory;

        private const int MinNameLength = 3;
        private const int MaxNameLength = 32;
        private const int MinTagLength = 2;
        private const int MaxTagLength = 6;
        private const int MaxDescriptionLength = 256;
        private const int DefaultMaxMembers = 50;
        private const int InviteLifetimeDays = 7;

        private static readonly Regex TagRegex = new Regex("^[A-Za-z0-9]+$", RegexOptions.Compiled);

        private IGroupsDatabaseAccessor databaseAccessor;
        private AuthModule authModule;
        private ChatModule chatModule;

        /// <summary>
        /// Gets or sets the factory used to register group persistence.
        /// </summary>
        public DatabaseAccessorFactory DatabaseAccessorFactory
        {
            get => databaseAccessorFactory;
            set => databaseAccessorFactory = value;
        }

        protected override void Awake()
        {
            base.Awake();

            AddDependency<AuthModule>();
            AddDependency<ChatModule>();
        }

        public override void Initialize(IServer server)
        {
            if (databaseAccessorFactory != null)
                databaseAccessorFactory.CreateAccessors();

            databaseAccessor = Mst.Server.DbAccessors.GetAccessor<IGroupsDatabaseAccessor>();
            authModule = server.GetModule<AuthModule>();
            chatModule = server.GetModule<ChatModule>();

            if (databaseAccessor == null)
                logger.Fatal($"{nameof(GroupModule)} requires {nameof(IGroupsDatabaseAccessor)}");

            if (authModule == null)
                logger.Fatal($"{nameof(GroupModule)} requires {nameof(AuthModule)}");

            if (chatModule == null)
                logger.Fatal($"{nameof(GroupModule)} requires {nameof(ChatModule)}");

            server.RegisterMessageHandler(MstOpCodes.ClientGroupCreate, ClientGroupCreateMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientGroupGetMine, ClientGroupGetMineMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientGroupInviteMember, ClientGroupInviteMemberMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientGroupGetInvites, ClientGroupGetInvitesMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientGroupAcceptInvite, ClientGroupAcceptInviteMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientGroupLeave, ClientGroupLeaveMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerGetPlayerGroup, ServerGetPlayerGroupMessageHandler);

            if (authModule != null)
                authModule.OnUserLoggedInEvent += AuthModule_OnUserLoggedInEvent;
        }

        private void OnDestroy()
        {
            if (authModule != null)
                authModule.OnUserLoggedInEvent -= AuthModule_OnUserLoggedInEvent;
        }

        private async Task ClientGroupCreateMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetUser(message, out var user))
                return;

            if (!TryGetDatabase(message))
                return;

            var packet = message.AsPacket<GroupCreatePacket>();

            if (!TryNormalizeCreatePacket(packet, out string errorCode))
            {
                logger.Warn($"Group create rejected. reason={errorCode}, userId={user.UserId}");
                message.RespondError(ResponseStatus.BadRequest, errorCode);
                return;
            }

            try
            {
                var existingMember = await databaseAccessor.GetMemberByAccountIdAsync(user.UserId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (existingMember != null)
                {
                    message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.GROUP_ALREADY_JOINED);
                    return;
                }

                var existingGroupByName = await databaseAccessor.GetGroupByNameAsync(packet.Name, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (existingGroupByName != null)
                {
                    message.RespondError(ResponseStatus.AlreadyExists, MstErrorCodes.GROUP_NAME_ALREADY_EXISTS);
                    return;
                }

                var existingGroupByTag = await databaseAccessor.GetGroupByTagAsync(packet.Tag, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (existingGroupByTag != null)
                {
                    message.RespondError(ResponseStatus.AlreadyExists, MstErrorCodes.GROUP_TAG_ALREADY_EXISTS);
                    return;
                }

                DateTime now = DateTime.UtcNow;
                string groupId = Guid.NewGuid().ToString();
                string channelName = CreateGroupChannelName(groupId);

                var group = new MstGroupInfo
                {
                    Id = groupId,
                    Name = packet.Name,
                    Tag = packet.Tag,
                    Description = packet.Description,
                    OwnerAccountId = user.UserId,
                    OwnerUsername = user.Username,
                    ChatChannelName = channelName,
                    JoinPolicy = (MstGroupJoinPolicy)packet.JoinPolicy,
                    MaxMembers = DefaultMaxMembers,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                var owner = new MstGroupMemberInfo
                {
                    GroupId = groupId,
                    AccountId = user.UserId,
                    Username = user.Username,
                    Role = MstGroupRole.Owner,
                    JoinedAtUtc = now,
                    LastSeenAtUtc = now
                };

                var created = await databaseAccessor.CreateGroupAsync(group, owner, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                AddOnlineUserToGroupChat(created, owner);

                logger.Info($"Group created. groupId={created.Id}, name={created.Name}, tag={created.Tag}, owner={user.Username}");
                message.Respond(GroupInfoPacket.FromGroup(created, owner), ResponseStatus.Success);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"Group create failed. userId={user.UserId}, error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        private async Task ClientGroupGetMineMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetUser(message, out var user))
                return;

            if (!TryGetDatabase(message))
                return;

            try
            {
                var member = await databaseAccessor.GetMemberByAccountIdAsync(user.UserId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (member == null)
                {
                    message.Respond(GroupInfoPacket.Empty(), ResponseStatus.Success);
                    return;
                }

                var group = await databaseAccessor.GetGroupByIdAsync(member.GroupId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                message.Respond(GroupInfoPacket.FromGroup(group, member), ResponseStatus.Success);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"Group get mine failed. userId={user.UserId}, error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        private async Task ClientGroupInviteMemberMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetUser(message, out var user))
                return;

            if (!TryGetDatabase(message))
                return;

            string targetUsername = (message.AsString() ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(targetUsername))
            {
                message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.GROUP_TARGET_USERNAME_REQUIRED);
                return;
            }

            try
            {
                var actorMember = await databaseAccessor.GetMemberByAccountIdAsync(user.UserId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (actorMember == null)
                {
                    message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.GROUP_MEMBERSHIP_REQUIRED);
                    return;
                }

                if (actorMember.Role < MstGroupRole.Officer)
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.GROUP_PERMISSION_REQUIRED);
                    return;
                }

                var group = await databaseAccessor.GetGroupByIdAsync(actorMember.GroupId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (group == null)
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.GROUP_NOT_FOUND);
                    return;
                }

                var targetAccount = await authModule.DatabaseAccessor.GetAccountByUsernameAsync(targetUsername, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (targetAccount == null)
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.GROUP_TARGET_ACCOUNT_NOT_FOUND);
                    return;
                }

                if (string.Equals(targetAccount.Id, user.UserId, StringComparison.OrdinalIgnoreCase))
                {
                    message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.GROUP_CANNOT_INVITE_SELF);
                    return;
                }

                var targetMember = await databaseAccessor.GetMemberByAccountIdAsync(targetAccount.Id, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (targetMember != null)
                {
                    message.RespondError(ResponseStatus.Conflict, MstErrorCodes.GROUP_TARGET_ALREADY_JOINED);
                    return;
                }

                var existingInvite = await databaseAccessor.GetPendingInviteAsync(group.Id, targetAccount.Id, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (existingInvite != null)
                {
                    message.RespondError(ResponseStatus.AlreadyExists, MstErrorCodes.GROUP_INVITE_ALREADY_EXISTS);
                    return;
                }

                DateTime now = DateTime.UtcNow;

                var invite = new MstGroupInviteInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = group.Id,
                    FromAccountId = user.UserId,
                    FromUsername = user.Username,
                    ToAccountId = targetAccount.Id,
                    ToUsername = targetAccount.Username,
                    Status = MstGroupInviteStatus.Pending,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = now.AddDays(InviteLifetimeDays),
                    UpdatedAtUtc = now
                };

                await databaseAccessor.CreateInviteAsync(invite, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                logger.Info($"Group invite created. groupId={group.Id}, from={user.Username}, to={targetAccount.Username}");
                message.Respond(CreateInvitePacket(invite, group), ResponseStatus.Success);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"Group invite failed. userId={user.UserId}, target={targetUsername}, error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        private async Task ClientGroupGetInvitesMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetUser(message, out var user))
                return;

            if (!TryGetDatabase(message))
                return;

            try
            {
                var invites = await databaseAccessor.GetPendingInvitesForAccountAsync(user.UserId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var packet = new GroupInviteListPacket();

                foreach (var invite in invites)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var group = await databaseAccessor.GetGroupByIdAsync(invite.GroupId, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (group != null)
                        packet.Items.Add(CreateInvitePacket(invite, group));
                }

                message.Respond(packet, ResponseStatus.Success);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"Group get invites failed. userId={user.UserId}, error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        private async Task ClientGroupAcceptInviteMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetUser(message, out var user))
                return;

            if (!TryGetDatabase(message))
                return;

            string inviteId = (message.AsString() ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(inviteId))
            {
                message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.GROUP_INVITE_ID_REQUIRED);
                return;
            }

            try
            {
                var existingMember = await databaseAccessor.GetMemberByAccountIdAsync(user.UserId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (existingMember != null)
                {
                    message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.GROUP_ALREADY_JOINED);
                    return;
                }

                var invite = await databaseAccessor.GetInviteByIdAsync(inviteId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (invite == null || invite.Status != MstGroupInviteStatus.Pending)
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.GROUP_INVITE_NOT_FOUND);
                    return;
                }

                if (!string.Equals(invite.ToAccountId, user.UserId, StringComparison.OrdinalIgnoreCase))
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.GROUP_INVITE_RECIPIENT_MISMATCH);
                    return;
                }

                if (invite.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.GROUP_INVITE_EXPIRED);
                    return;
                }

                var group = await databaseAccessor.GetGroupByIdAsync(invite.GroupId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (group == null)
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.GROUP_NOT_FOUND);
                    return;
                }

                int membersCount = await databaseAccessor.CountMembersAsync(group.Id, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (group.MaxMembers > 0 && membersCount >= group.MaxMembers)
                {
                    message.RespondError(ResponseStatus.Conflict, MstErrorCodes.GROUP_FULL);
                    return;
                }

                DateTime now = DateTime.UtcNow;
                var member = new MstGroupMemberInfo
                {
                    GroupId = group.Id,
                    AccountId = user.UserId,
                    Username = user.Username,
                    Role = MstGroupRole.Member,
                    JoinedAtUtc = now,
                    LastSeenAtUtc = now
                };

                await databaseAccessor.AcceptInviteAsync(invite.Id, member, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                AddOnlineUserToGroupChat(group, member);

                logger.Info($"Group invite accepted. groupId={group.Id}, user={user.Username}");
                message.Respond(GroupInfoPacket.FromGroup(group, member), ResponseStatus.Success);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"Group invite accept failed. userId={user.UserId}, inviteId={inviteId}, error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        private async Task ClientGroupLeaveMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetUser(message, out var user))
                return;

            if (!TryGetDatabase(message))
                return;

            try
            {
                var member = await databaseAccessor.GetMemberByAccountIdAsync(user.UserId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (member == null)
                {
                    message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.GROUP_MEMBERSHIP_REQUIRED);
                    return;
                }

                if (member.Role == MstGroupRole.Owner)
                {
                    message.RespondError(ResponseStatus.Conflict, MstErrorCodes.GROUP_OWNER_MUST_TRANSFER_OR_DISBAND);
                    return;
                }

                var group = await databaseAccessor.GetGroupByIdAsync(member.GroupId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await databaseAccessor.RemoveMemberAsync(member.GroupId, user.UserId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (group != null)
                    RemoveOnlineUserFromGroupChat(group, user.Username);

                logger.Info($"Group member left. groupId={member.GroupId}, user={user.Username}");
                message.Respond(ResponseStatus.Success);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"Group leave failed. userId={user.UserId}, error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        private async Task ServerGetPlayerGroupMessageHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetRegisteredRoomPeer(message))
                return;

            if (!TryGetDatabase(message))
                return;

            string userId = (message.AsString() ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(userId))
            {
                message.RespondError(ResponseStatus.BadRequest, MstErrorCodes.GROUP_PLAYER_ACCOUNT_ID_REQUIRED);
                return;
            }

            try
            {
                var member = await databaseAccessor.GetMemberByAccountIdAsync(userId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (member == null)
                {
                    message.Respond(GroupInfoPacket.Empty(), ResponseStatus.Success);
                    return;
                }

                var group = await databaseAccessor.GetGroupByIdAsync(member.GroupId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                message.Respond(GroupInfoPacket.FromGroup(group, member), ResponseStatus.Success);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"Room get player group failed. userId={userId}, error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }

            return;
        }

        private bool TryGetUser(IIncomingMessage message, out IUserPeerExtension user)
        {
            user = message.Peer.GetExtension<IUserPeerExtension>();

            if (user?.Account != null && !string.IsNullOrWhiteSpace(user.UserId))
                return true;

            message.RespondError(ResponseStatus.Unauthorized, MstErrorCodes.USER_IS_NOT_LOGGED_IN);
            return false;
        }

        private bool TryGetDatabase(IIncomingMessage message)
        {
            if (databaseAccessor != null)
                return true;

            logger.Error("Group request rejected. reason=database_accessor_not_found");
            message.RespondError(ResponseStatus.ServiceUnavailable, MstErrorCodes.GROUP_DATABASE_UNAVAILABLE);
            return false;
        }

        private bool TryGetRegisteredRoomPeer(IIncomingMessage message)
        {
            if (message?.Peer?.GetProperty(MstPeerPropertyCodes.RegisteredRooms) is IReadOnlyDictionary<int, RegisteredRoom> rooms &&
                rooms.Count > 0)
            {
                return true;
            }

            logger.Warn($"Group room API rejected. reason=not_registered_room_peer, peerId={message?.Peer?.Id}");
            message?.RespondError(ResponseStatus.Forbidden, MstErrorCodes.GROUP_ROOM_PEER_FORBIDDEN);
            return false;
        }

        private bool TryNormalizeCreatePacket(GroupCreatePacket packet, out string errorCode)
        {
            errorCode = string.Empty;

            if (packet == null)
            {
                errorCode = MstErrorCodes.GROUP_BAD_PACKET;
                return false;
            }

            packet.Name = (packet.Name ?? string.Empty).Trim();
            packet.Tag = (packet.Tag ?? string.Empty).Trim().ToUpperInvariant();
            packet.Description = (packet.Description ?? string.Empty).Trim();

            if (packet.Name.Length < MinNameLength || packet.Name.Length > MaxNameLength)
            {
                errorCode = MstErrorCodes.GROUP_NAME_LENGTH_INVALID;
                return false;
            }

            if (packet.Tag.Length < MinTagLength || packet.Tag.Length > MaxTagLength || !TagRegex.IsMatch(packet.Tag))
            {
                errorCode = MstErrorCodes.GROUP_TAG_INVALID;
                return false;
            }

            if (packet.Description.Length > MaxDescriptionLength)
            {
                errorCode = MstErrorCodes.GROUP_DESCRIPTION_LENGTH_INVALID;
                return false;
            }

            if (!Enum.IsDefined(typeof(MstGroupJoinPolicy), packet.JoinPolicy))
            {
                errorCode = MstErrorCodes.GROUP_JOIN_POLICY_INVALID;
                return false;
            }

            return true;
        }

        private Task AuthModule_OnUserLoggedInEvent(IUserPeerExtension user,
            CancellationToken cancellationToken)
        {
            return RestoreGroupChatMembershipAsync(user, cancellationToken);
        }

        private async Task RestoreGroupChatMembershipAsync(IUserPeerExtension user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (databaseAccessor == null || chatModule == null || user?.Account == null)
                return;

            var member = await databaseAccessor.GetMemberByAccountIdAsync(user.UserId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (member == null)
                return;

            var group = await databaseAccessor.GetGroupByIdAsync(member.GroupId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (group == null)
                return;

            AddOnlineUserToGroupChat(group, member);
            logger.Info($"Group chat membership restored. groupId={group.Id}, user={member.Username}");
        }

        private void EnsureGroupChatChannel(MstGroupInfo group)
        {
            chatModule?.GetOrCreateChannel(group.ChatChannelName, new ChatChannelOptions
            {
                Type = ChatChannelType.PrivateGroup,
                OwnerUsername = group.OwnerUsername,
                OwnerId = group.Id,
                OwnerService = nameof(GroupModule),
                IsClosed = true,
                IsPersistent = true,
                MaxHistory = 100,
                HistoryVisibility = ChatHistoryVisibility.MembersOnly,
                DefaultMemberPermissions = ChatChannelPermission.DefaultMember,
                InvitedMemberPermissions = ChatChannelPermission.DefaultMember,
                OwnerPermissions = ChatChannelPermission.Owner
            });
        }

        private void AddOnlineUserToGroupChat(MstGroupInfo group, MstGroupMemberInfo member)
        {
            if (chatModule == null || group == null || member == null)
                return;

            EnsureGroupChatChannel(group);

            var permissions = member.Role == MstGroupRole.Owner
                ? ChatChannelPermission.Owner
                : member.Role == MstGroupRole.Officer
                    ? ChatChannelPermission.Moderator
                    : ChatChannelPermission.DefaultMember;

            chatModule.AddUserToChannel(group.ChatChannelName, member.Username, permissions);
        }

        private void RemoveOnlineUserFromGroupChat(MstGroupInfo group, string username)
        {
            if (chatModule == null || group == null || string.IsNullOrWhiteSpace(username))
                return;

            chatModule.RemoveUserFromChannel(group.ChatChannelName, username);
        }

        private static string CreateGroupChannelName(string groupId)
        {
            string compactId = (groupId ?? Guid.NewGuid().ToString()).Replace("-", string.Empty);

            if (compactId.Length > 16)
                compactId = compactId.Substring(0, 16);

            return $"group_{compactId}";
        }

        private static GroupInvitePacket CreateInvitePacket(MstGroupInviteInfo invite, MstGroupInfo group)
        {
            return new GroupInvitePacket
            {
                InviteId = invite.Id,
                GroupId = invite.GroupId,
                GroupName = group?.Name ?? string.Empty,
                GroupTag = group?.Tag ?? string.Empty,
                FromUsername = invite.FromUsername,
                ToUsername = invite.ToUsername
            };
        }
    }
}
