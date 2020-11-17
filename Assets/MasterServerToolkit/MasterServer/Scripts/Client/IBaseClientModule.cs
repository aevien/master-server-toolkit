using MasterServerToolkit.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public interface IBaseClientModule
    {
        IMstBaseClient ClientBehaviour { get; set; }
        void OnInitialize(IMstBaseClient clientBehaviour);
    }
}