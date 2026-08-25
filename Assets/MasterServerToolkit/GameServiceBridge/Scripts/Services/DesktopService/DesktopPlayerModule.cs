using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class DesktopPlayerModule : BasePlayerModule
    {
        private const string PlayerNameKeySuffix = "PlayerNameKey";
        private const string PlayerIdKeySuffix = "PlayerIdKey";

        public override void OnBeforeInit(IService service)
        {
            base.OnBeforeInit(service);
            IsSupported = true;
            GetOrCreatePlayer();
        }

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public override void Authenticate(SuccessCallback callback)
        {
            base.Authenticate(callback);
            NotifyOnAuthenticated(false, $"{Service.Id} guest service does not support platform authentication");
        }

        private void GetOrCreatePlayer()
        {
            Id = PlayerPrefs.GetString(PlayerIdKey, Guid.NewGuid().ToString());
            Name = PlayerPrefs.GetString(PlayerNameKey, "Guest Player");
            Avatar = string.Empty;
            IsGuest = true;
            Extra = MstJson.CreateObject();

            PlayerPrefs.SetString(PlayerIdKey, Id);
            PlayerPrefs.SetString(PlayerNameKey, Name);
            PlayerPrefs.Save();
        }

        private string KeyPrefix => (Service?.Id ?? GameServiceId.Desktop).ToString().ToLowerInvariant();
        private string PlayerIdKey => $"{KeyPrefix}{PlayerIdKeySuffix}";
        private string PlayerNameKey => $"{KeyPrefix}{PlayerNameKeySuffix}";
    }
}
