using MasterServerToolkit.Logging;
using MasterServerToolkit.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class RoomClient<T> : Singleton<T> where T : MonoBehaviour
    {
        /// <summary>
        /// Latest access data. When switching scenes, if this is set,
        /// connector should most likely try to use this data to connect to game server
        /// (if the scene is right)
        /// </summary>
        protected static RoomAccessPacket AccessData;

        protected override void Awake()
        {
            base.Awake();

            // Register access listener
            Mst.Client.Rooms.OnAccessReceivedEvent += OnAccessReceivedEvent;
        }

        protected virtual void OnDestroy()
        {
            Mst.Client.Rooms.OnAccessReceivedEvent -= OnAccessReceivedEvent;
        }

        /// <summary>
        /// Invoked when room access received
        /// </summary>
        /// <param name="access"></param>
        private void OnAccessReceivedEvent(RoomAccessPacket access)
        {
            StartConnection(access);
        }

        /// <summary>
        /// Starts connection process
        /// </summary>
        /// <param name="access"></param>
        protected abstract void StartConnection(RoomAccessPacket access);

        /// <summary>
        /// Starts connection process
        /// </summary>
        /// <param name="access"></param>
        public static void Connect(RoomAccessPacket access)
        {
            if (Instance == null)
            {
                Logs.Error("Failed to connect to game server. No Game Connector was found in the scene");
                return;
            }

            // Save the access data
            AccessData = access;

            // Start connection
            (Instance as RoomClient<T>).StartConnection(access);
        }
    }
}