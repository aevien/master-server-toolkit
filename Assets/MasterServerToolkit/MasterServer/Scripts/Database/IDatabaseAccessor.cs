using MasterServerToolkit.Logging;
using System;

namespace MasterServerToolkit.MasterServer
{
    public interface IDatabaseAccessor : IDisposable
    {
        MstProperties CustomProperties { get; }
        Logger Logger { get; set; }
    }
}