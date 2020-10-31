namespace MasterServerToolkit.MasterServer
{
    public interface IHttpController
    {
        HttpServerModule Server { get; set; }
        void Initialize(HttpServerModule server);
    }
}
