using MasterServerToolkit.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class NotificationRecipient
    {
        public string UserId { get; set; }
        public IPeer Peer { get; set; }

        public NotificationRecipient(string userId, IPeer peer)
        {
            UserId = userId;
            Peer = peer;
        }

        public void Notify(string message)
        {
            Peer.SendMessage((short)MstMessageCodes.Notification, message);
        }
    }
}