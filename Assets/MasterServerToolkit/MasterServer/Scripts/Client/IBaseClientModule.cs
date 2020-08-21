using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public interface IBaseClientModule
    {
        IBaseClientBehaviour ClientBehaviour { get; set; }
        void OnInitialize(IBaseClientBehaviour clientBehaviour);
    }
}