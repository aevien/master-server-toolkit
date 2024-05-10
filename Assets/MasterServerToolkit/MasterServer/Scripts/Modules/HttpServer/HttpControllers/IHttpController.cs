using System;

namespace MasterServerToolkit.MasterServer
{
    public interface IHttpController : IDisposable
    {
        bool UseCredentials { get; set; }
        HttpServerModule HttpServer { get; set; }
        ServerBehaviour MasterServer { get; set; }
        void Initialize(HttpServerModule server);
    }
}
