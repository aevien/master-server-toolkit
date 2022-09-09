using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public interface IDatabaseAccessor : IDisposable
    {
        MstProperties CustomProperties { get; }
    }
}