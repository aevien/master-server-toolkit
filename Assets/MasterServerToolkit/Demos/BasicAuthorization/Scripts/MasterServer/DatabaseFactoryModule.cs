using System;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using LiteDB;
#endif

namespace MasterServerToolkit.MasterServer.Examples.BasicAuthorization
{
    public class DatabaseFactoryModule : BaseServerModule
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up database accessors for the game"
        };

        public override void Initialize(IServer server)
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            try
            {
                Mst.Server.DbAccessors.SetAccessor<IAccountsDatabaseAccessor>(new AccountsDatabaseAccessor(new LiteDatabase(@"accounts.db")));
            }
            catch (Exception e)
            {
                logger.Error("Failed to setup LiteDB");
                logger.Error(e);
            }
#endif
        }
    }
}