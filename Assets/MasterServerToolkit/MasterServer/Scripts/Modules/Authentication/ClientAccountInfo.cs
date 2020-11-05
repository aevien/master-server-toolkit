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
        public bool IsDirty { get; private set; }

        event Action<ClientAccountInfo> OnChangedEvent;
        public ClientAccountInfo()
        {
            Id = string.Empty;
            Username = string.Empty;
            Password = string.Empty;
            Email = string.Empty;
            PhoneNumber = string.Empty;
            Facebook = string.Empty;
            Token = string.Empty;
            IsAdmin = false;
            IsGuest = true;
            IsEmailConfirmed = false;
            Properties = new MstProperties();
        }

        public void MarkAsDirty(bool value = true)
        {
            IsDirty = value;
            OnChangedEvent?.Invoke(this);
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("Id", Id);
            options.Add("Username", Username);
            options.Add("Email", Email);
            options.Add("PhoneNumber", PhoneNumber);
            options.Add("Facebook", Facebook);
            options.Add("Token", Token);
            options.Add("IsAdmin", IsAdmin);
            options.Add("IsGuest", IsGuest);
            options.Add("IsEmailConfirmed", IsEmailConfirmed);
            options.Append(Properties);

            return options.ToReadableString(";\n");
        }
    }
}