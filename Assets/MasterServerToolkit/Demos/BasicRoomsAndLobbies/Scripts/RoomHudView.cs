using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicSpawner
{
    public class RoomHudView : UIView
    {
        public void Disconnect()
        {
            RoomClientManager.Disconnect();
        }
    }
}