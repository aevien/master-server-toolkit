using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class ClientAccountInfo
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Facebook { get; set; }
        public string Token { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsGuest { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public MstProperties Properties { get; set; }

        event Action<ClientAccountInfo> OnChangedEvent;

        public void MarkAsDirty()
        {
            OnChangedEvent?.Invoke(this);
        }
    }
}