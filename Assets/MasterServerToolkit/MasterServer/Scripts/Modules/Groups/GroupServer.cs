using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class GroupServer : MstBaseClient
    {
        public GroupServer(IClientSocket connection) : base(connection) { }

        public void GetPlayerGroup(string userId, GroupResultCallback<GroupInfoPacket> callback)
        {
            Connection.SendMessage(MstOpCodes.ServerGetPlayerGroup, userId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(default, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(response.AsPacket<GroupInfoPacket>(), string.Empty);
            });
        }
    }
}
