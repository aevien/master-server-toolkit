using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class QuestsModuleServer : MstBaseClient
    {
        public QuestsModuleServer(IClientSocket connection) : base(connection)
        {
        }
    }
}
