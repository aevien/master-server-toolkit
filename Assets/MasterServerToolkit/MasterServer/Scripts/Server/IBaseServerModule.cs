using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public interface IBaseServerModule
    {
        List<Type> Dependencies { get; }
        List<Type> OptionalDependencies { get; }
        ServerBehaviour Server { get; set; }
        void Initialize(IServer server);
        MstProperties Info();
        JObject JsonInfo();
    }
}