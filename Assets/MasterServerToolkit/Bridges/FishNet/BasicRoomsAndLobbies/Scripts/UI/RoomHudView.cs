#if FISHNET
using MasterServerToolkit.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Bridges.FishNetworking
{
    public class RoomHudView : UIView
    {
        public void Disconnect()
        {
            RoomClientManager.Disconnect();
        }
    }
}
#endif