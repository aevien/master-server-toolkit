using MasterServerToolkit.Json;
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
        MstJson JsonInfo();
    }
}