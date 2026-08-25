using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class GroupClient : MstBaseClient
    {
        public GroupClient(IClientSocket connection) : base(connection)
        {
            RegisterErrors(
                MstErrorCodes.GROUP_ALREADY_JOINED,
                MstErrorCodes.GROUP_BAD_PACKET,
                MstErrorCodes.GROUP_CANNOT_INVITE_SELF,
                MstErrorCodes.GROUP_DATABASE_UNAVAILABLE,
                MstErrorCodes.GROUP_DESCRIPTION_LENGTH_INVALID,
                MstErrorCodes.GROUP_FULL,
                MstErrorCodes.GROUP_INVITE_ALREADY_EXISTS,
                MstErrorCodes.GROUP_INVITE_EXPIRED,
                MstErrorCodes.GROUP_INVITE_ID_REQUIRED,
                MstErrorCodes.GROUP_INVITE_NOT_FOUND,
                MstErrorCodes.GROUP_INVITE_RECIPIENT_MISMATCH,
                MstErrorCodes.GROUP_JOIN_POLICY_INVALID,
                MstErrorCodes.GROUP_MEMBERSHIP_REQUIRED,
                MstErrorCodes.GROUP_NAME_ALREADY_EXISTS,
                MstErrorCodes.GROUP_NAME_LENGTH_INVALID,
                MstErrorCodes.GROUP_NOT_FOUND,
                MstErrorCodes.GROUP_OWNER_MUST_TRANSFER_OR_DISBAND,
                MstErrorCodes.GROUP_PERMISSION_REQUIRED,
                MstErrorCodes.GROUP_PLAYER_ACCOUNT_ID_REQUIRED,
                MstErrorCodes.GROUP_ROOM_PEER_FORBIDDEN,
                MstErrorCodes.GROUP_TAG_ALREADY_EXISTS,
                MstErrorCodes.GROUP_TAG_INVALID,
                MstErrorCodes.GROUP_TARGET_ACCOUNT_NOT_FOUND,
                MstErrorCodes.GROUP_TARGET_ALREADY_JOINED,
                MstErrorCodes.GROUP_TARGET_USERNAME_REQUIRED,
                MstErrorCodes.USER_IS_NOT_LOGGED_IN);
        }

        public void CreateGroup(GroupCreatePacket packet, GroupResultCallback<GroupInfoPacket> callback)
        {
            Connection.SendMessage(MstOpCodes.ClientGroupCreate, packet, (status, response) =>
            {
                HandlePacketResponse(status, response, callback);
            });
        }

        public void GetMyGroup(GroupResultCallback<GroupInfoPacket> callback)
        {
            Connection.SendMessage(MstOpCodes.ClientGroupGetMine, (status, response) =>
            {
                HandlePacketResponse(status, response, callback);
            });
        }

        public void InviteMember(string username, GroupResultCallback<GroupInvitePacket> callback)
        {
            Connection.SendMessage(MstOpCodes.ClientGroupInviteMember, username, (status, response) =>
            {
                HandlePacketResponse(status, response, callback);
            });
        }

        public void GetInvites(GroupResultCallback<GroupInviteListPacket> callback)
        {
            Connection.SendMessage(MstOpCodes.ClientGroupGetInvites, (status, response) =>
            {
                HandlePacketResponse(status, response, callback);
            });
        }

        public void AcceptInvite(string inviteId, GroupResultCallback<GroupInfoPacket> callback)
        {
            Connection.SendMessage(MstOpCodes.ClientGroupAcceptInvite, inviteId, (status, response) =>
            {
                HandlePacketResponse(status, response, callback);
            });
        }

        public void LeaveGroup(SuccessCallback callback)
        {
            Connection.SendMessage(MstOpCodes.ClientGroupLeave, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(true, string.Empty);
            });
        }

        private void HandlePacketResponse<T>(ResponseStatus status, IIncomingMessage response, GroupResultCallback<T> callback)
            where T : SerializablePacket, new()
        {
            if (status != ResponseStatus.Success)
            {
                callback?.Invoke(default, Mst.Errors.Parse(status, response));
                return;
            }

            callback?.Invoke(response.AsPacket<T>(), string.Empty);
        }
    }
}
