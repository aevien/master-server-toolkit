using MasterServerToolkit.Json;
using System;

namespace MasterServerToolkit.MasterServer
{
    public interface IWebController : IDisposable
    {
        bool UseCredentials { get; set; }
        HttpServerModule WebServer { get; set; }
        ServerBehaviour Server { get; set; }
        void Initialize(HttpServerModule server);
        MstJson JsonInfo();
    }
}