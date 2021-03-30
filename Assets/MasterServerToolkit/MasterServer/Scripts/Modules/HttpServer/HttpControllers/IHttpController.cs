namespace MasterServerToolkit.MasterServer
{
    public interface IHttpController
    {
        HttpServerModule HttpServer { get; set; }
        ServerBehaviour MasterServer { get; set; }
        void Initialize(HttpServerModule server);
    }
}
