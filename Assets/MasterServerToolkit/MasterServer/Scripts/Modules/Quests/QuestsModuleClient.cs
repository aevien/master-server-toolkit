using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class QuestsModuleClient : MstBaseClient
    {
        public QuestsModuleClient(IClientSocket connection) : base(connection)
        {
            Mst.Errors.TryRegister(MstErrorCodes.USER_IS_NOT_LOGGED_IN);
            Mst.Errors.TryRegister(MstErrorCodes.QUEST_UPDATE_FORBIDDEN);
        }
    }
}
