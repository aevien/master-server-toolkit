using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class EditorPlayerModule : BasePlayerModule
    {
        private const string GuestPlayerInfoKey = "playerGuestInfoKey";
        private const string GuestAvatarUrl = "https://i.pravatar.cc/300?img=18";
        private const string AuthenticatedAvatarUrl = "https://i.pravatar.cc/300?img=8";

        private string authenticatedUserId = string.Empty;
        private string authenticatedUserDisplayName = string.Empty;

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = true;
            MstJson authenticationSupport = Service.Options
                .GetField(nameof(EditorSdkSettings.interactiveAuthenticationSupported));
            IsAuthenticationSupported = authenticationSupport == null || authenticationSupport.BoolValue;
            authenticatedUserId = Service.Options.GetField(nameof(EditorSdkSettings.userId)).StringValue;
            authenticatedUserDisplayName = Service.Options.GetField(nameof(EditorSdkSettings.authenticatedDisplayName)).StringValue;

            if (Service.Options.GetField(nameof(EditorSdkSettings.startAsGuest)).BoolValue)
                LoadOrCreateGuestIdentity();
            else
                ApplyAuthenticatedIdentity();

            IsReady = true;
        }

        public override void Authenticate(SuccessCallback callback)
        {
            base.Authenticate(callback);

            if (!IsAuthenticationSupported)
            {
                NotifyOnAuthenticated(false, "Editor interactive platform authentication is disabled");
                return;
            }

            StartCoroutine(AuthenticateCoroutine());
        }

        private IEnumerator AuthenticateCoroutine()
        {
            yield return new WaitForSecondsRealtime(0.5f);

            ApplyAuthenticatedIdentity();
            Service.Storage?.LoadData(null);
            NotifyOnAuthenticated(true, string.Empty);
            NotifyOnInfoChanged();
        }

        private void LoadOrCreateGuestIdentity()
        {
            try
            {
                string rawPlayerInfo = PlayerPrefs.GetString(GuestPlayerInfoKey, string.Empty);

                if (!string.IsNullOrWhiteSpace(rawPlayerInfo) && MstJson.IsJson(rawPlayerInfo))
                {
                    var playerInfo = new MstJson(rawPlayerInfo);

                    if (playerInfo.IsObject &&
                        playerInfo.HasField("id") &&
                        !string.IsNullOrWhiteSpace(playerInfo["id"].StringValue))
                    {
                        ApplyIdentity(
                            playerInfo["id"].StringValue,
                            string.Empty,
                            playerInfo.HasField("avatar") ? playerInfo["avatar"].StringValue : GuestAvatarUrl,
                            true,
                            playerInfo.HasField("extra") && playerInfo["extra"].IsObject
                                ? playerInfo["extra"]
                                : MstJson.CreateObject());
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to load Editor guest identity: {exception}");
            }

            CreateGuestIdentity();
        }

        private void CreateGuestIdentity()
        {
            ApplyIdentity(
                Guid.NewGuid().ToString(),
                string.Empty,
                GuestAvatarUrl,
                true,
                MstJson.CreateObject());

            var playerInfo = MstJson.CreateObject();
            playerInfo.AddField("id", Id);
            playerInfo.AddField("avatar", Avatar);
            playerInfo.AddField("extra", Extra);

            try
            {
                PlayerPrefs.SetString(GuestPlayerInfoKey, playerInfo.ToString());
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to save Editor guest identity: {exception}");
            }
        }

        private void ApplyAuthenticatedIdentity()
        {
            ApplyIdentity(
                authenticatedUserId,
                authenticatedUserDisplayName,
                AuthenticatedAvatarUrl,
                false,
                MstJson.CreateObject());
        }

        private void ApplyIdentity(string id, string name, string avatar, bool isGuest, MstJson extra)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Avatar = avatar ?? string.Empty;
            IsGuest = isGuest;
            Extra = extra ?? MstJson.CreateObject();
        }
    }
}
