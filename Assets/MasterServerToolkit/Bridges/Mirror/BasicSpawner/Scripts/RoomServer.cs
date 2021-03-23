using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomServer : NetworkBehaviour
    {

        public override void OnStartServer()
        {
            Debug.Log("Room server started");
        }

        public override void OnStopServer()
        {
            Debug.Log("Room server stopped");
        }
    }
}