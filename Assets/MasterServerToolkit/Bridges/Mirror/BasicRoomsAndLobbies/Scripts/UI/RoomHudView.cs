using MasterServerToolkit.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomHudView : UIView
    {
        public void Disconnect()
        {
            RoomClientManager.Disconnect();
        }
    }
}