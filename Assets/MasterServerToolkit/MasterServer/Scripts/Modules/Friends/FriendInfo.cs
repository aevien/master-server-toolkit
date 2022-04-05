using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class FriendInfo
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Avatar { get; set; }
        public MstProperties Properties { get; set; }
    }
}