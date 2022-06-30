using System;

namespace MasterServerToolkit.MasterServer
{
    public interface IHttpController : IDisposable
    {
        HttpServerModule HttpServer { get; set; }
        ServerBehaviour MasterServer { get; set; }
        void Initialize(HttpServerModule server);
    }
}
