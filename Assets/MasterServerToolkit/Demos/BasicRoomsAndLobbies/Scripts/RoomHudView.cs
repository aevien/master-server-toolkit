using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicSpawner
{
    public class RoomHudView : UIView
    {
        private RoomClientManager roomClientManager;

        protected override void Start()
        {
            base.Start();

            roomClientManager = FindObjectOfType<RoomClientManager>();
        }

        public void Disconnect()
        {
            roomClientManager?.Disconnect();
        }
    }
}