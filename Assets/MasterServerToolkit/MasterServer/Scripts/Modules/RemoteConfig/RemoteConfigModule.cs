using MasterServerToolkit.Networking;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public class RemoteConfigModule : BaseServerModule
    {
        protected readonly MstProperties configs = new();

        public override void Initialize(IServer server)
        {
            Set(Mst.Args.Names.AdminId, Mst.Args.AdminId);
            Set(Mst.Args.Names.AdminUsername, Mst.Args.AdminUsername);
            Set(Mst.Args.Names.UseDevMode, Mst.Args.UseDevMode);

            server.RegisterMessageHandler(MstOpCodes.GetRemoteConfig, GetRemoteConfigMessageHandler);
        }

        public MstProperties Set(string key, object value)
        {
            configs.Set(key, value);
            return configs;
        }

        public MstProperties Remove(string key)
        {
            configs.Remove(key);
            return configs;
        }

        #region MESSAGE_HANDLERS

        private Task GetRemoteConfigMessageHandler(IIncomingMessage message)
        {
            message.Respond(configs.ToBytes(), ResponseStatus.Success);
            return Task.CompletedTask;
        }

        #endregion
    }
}
