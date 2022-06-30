namespace MasterServerToolkit.MasterServer
{
    public interface IWsController
    {
        HttpServerModule HttpServer { get; set; }
        ServerBehaviour MasterServer { get; set; }
        WsControllerService WsService { get; set; }
        void Initialize(HttpServerModule httpServer, WsControllerService wsService);
    }
}