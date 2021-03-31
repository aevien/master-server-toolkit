using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class RoomPlayerPeerExtension : IRoomPlayerPeerExtension
    {
        /// <summary>
        /// Instance of room server peer extension
        /// </summary>
        /// <param name="masterPeerId"></param>
        /// <param name="username"></param>
        /// <param name="roomPeer"></param>
        /// <param name="customOptions"></param>
        public RoomPlayerPeerExtension(int masterPeerId, string username, IPeer roomPeer, MstProperties customOptions)
        {
            MasterPeerId = masterPeerId;
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Peer = roomPeer ?? throw new ArgumentNullException(nameof(roomPeer));
            Properties = customOptions ?? throw new ArgumentNullException(nameof(customOptions));
        }

        /// <summary>
        /// Peer Id of master server client
        /// </summary>
        public int MasterPeerId { get; }

        /// <summary>
        /// Room server peer
        /// </summary>
        public IPeer Peer { get; }

        /// <summary>
        /// Username
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Account properties
        /// </summary>
        public MstProperties Properties { get; }
    }
}
