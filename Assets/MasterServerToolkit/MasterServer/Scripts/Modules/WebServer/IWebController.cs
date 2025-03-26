using MasterServerToolkit.Json;
using System;

namespace MasterServerToolkit.MasterServer
{
    public interface IWebController : IDisposable
    {
        bool UseCredentials { get; set; }
        WebServerModule WebServer { get; set; }
        ServerBehaviour MasterServer { get; set; }
        void Initialize(WebServerModule server);
        MstJson JsonInfo();
    }
}