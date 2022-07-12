using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public interface IDatabaseAccessor
    {
        MstProperties CustomProperties { get; }
    }
}