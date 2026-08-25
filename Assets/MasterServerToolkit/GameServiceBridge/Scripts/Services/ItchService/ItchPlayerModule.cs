using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.GameService
{
    public class ItchPlayerModule : BasePlayerModule
    {
        private const string GuestPlayerIdKey = "mst_itch_guest_player_id";
        private const string GuestPlayerName = "Itch Guest";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string Gb_Itch_GetApiKey();

        [DllImport("__Internal")]
        private static extern string Gb_Itch_GetApiKeyExpiresAt();

#endif

        private string apiKey = string.Empty;
        private string apiKeyExpiresAt = string.Empty;

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = true;
            IsAuthenticationSupported = false;
            StartCoroutine(ResolvePlayerInfo());
        }

        public override void Authenticate(SuccessCallback callback)
        {
            base.Authenticate(callback);

            NotifyOnAuthenticated(false, "Itch interactive authentication is not supported");
        }

        private IEnumerator ResolvePlayerInfo()
        {
            Extra = MstJson.CreateObject();

            ItchAuthMode authMode = ItchService.GetAuthMode(Service.Options);

            if (authMode == ItchAuthMode.Disabled)
            {
                SetGuestPlayer("disabled");
                yield break;
            }

            apiKey = ResolveApiKey(authMode);
            apiKeyExpiresAt = ResolveApiKeyExpiresAt(authMode);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                SetGuestPlayer(authMode == ItchAuthMode.OAuth ? "oauth_pending" : "missing_api_key");
                yield break;
            }

            string profileUrl = ItchService.GetStringOption(
                Service.Options,
                nameof(ItchSdkSettings.profileUrl),
                ItchService.DefaultProfileUrl);

            int timeout = Math.Max(2, ItchService.GetIntOption(
                Service.Options,
                nameof(ItchSdkSettings.profileRequestTimeout),
                8));

            using (UnityWebRequest request = UnityWebRequest.Get(profileUrl))
            {
                request.timeout = timeout;
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                yield return request.SendWebRequest();

                if (RequestFailed(request))
                {
                    Logger.Warn($"Itch profile request failed. Error={request.error}, ResponseCode={request.responseCode}");
                    SetGuestPlayer("profile_request_failed");
                    yield break;
                }

                if (!TryApplyProfile(request.downloadHandler.text))
                {
                    Logger.Warn("Itch profile response did not contain a valid user id.");
                    SetGuestPlayer("invalid_profile_response");
                    yield break;
                }
            }
        }

        private string ResolveApiKey(ItchAuthMode authMode)
        {
            if (authMode == ItchAuthMode.OAuth)
                return string.Empty;

#if UNITY_EDITOR
            if (Service.Options.GetField(nameof(ItchSdkSettings.detectInEditor))?.BoolValue == true)
            {
                string editorApiKey = Service.Options.GetField(nameof(ItchSdkSettings.editorApiKey))?.StringValue;

                if (!string.IsNullOrWhiteSpace(editorApiKey))
                    return editorApiKey;
            }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            return Gb_Itch_GetApiKey();
#else
            string envName = ItchService.GetStringOption(
                Service.Options,
                nameof(ItchSdkSettings.apiKeyEnvironmentVariable),
                ItchService.DefaultApiKeyEnvironmentVariable);

            return ItchService.GetEnvironmentValue(envName, ItchService.DefaultApiKeyEnvironmentVariable);
#endif
        }

        private string ResolveApiKeyExpiresAt(ItchAuthMode authMode)
        {
            if (authMode == ItchAuthMode.OAuth)
                return string.Empty;

#if UNITY_WEBGL && !UNITY_EDITOR
            return Gb_Itch_GetApiKeyExpiresAt();
#else
            string envName = ItchService.GetStringOption(
                Service.Options,
                nameof(ItchSdkSettings.apiKeyExpiresAtEnvironmentVariable),
                ItchService.DefaultApiKeyExpiresAtEnvironmentVariable);

            return ItchService.GetEnvironmentValue(envName, ItchService.DefaultApiKeyExpiresAtEnvironmentVariable);
#endif
        }

        private bool TryApplyProfile(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson) || !MstJson.IsJson(rawJson))
                return false;

            MstJson profile = new MstJson(rawJson);
            MstJson user = profile.GetField("user") ?? profile;

            if (user == null || !TryReadString(user, "id", out string userId) || string.IsNullOrWhiteSpace(userId))
                return false;

            Id = userId;
            Name = ResolveDisplayName(user, userId);
            Avatar = ResolveAvatar(user);
            IsGuest = false;
            Extra = CreateExtraData("app_token");
            IsReady = true;

            Logger.Info($"Itch player resolved. UserId={Id}, DisplayName={Name}, AvatarPresent={!string.IsNullOrWhiteSpace(Avatar)}");
            NotifyOnInfoChanged();
            return true;
        }

        private void SetGuestPlayer(string reason)
        {
            Id = PlayerPrefs.GetString(GuestPlayerIdKey, Guid.NewGuid().ToString());
            PlayerPrefs.SetString(GuestPlayerIdKey, Id);
            PlayerPrefs.Save();
            Name = GuestPlayerName;
            Avatar = string.Empty;
            IsGuest = true;
            Extra = MstJson.CreateObject();
            Extra.SetField("auth_source", reason);
            IsReady = true;

            Logger.Warn($"Itch player is guest. Reason={reason}");
            NotifyOnInfoChanged();
        }

        private MstJson CreateExtraData(string source)
        {
            var extra = MstJson.CreateObject();
            extra.SetField(GameServiceKeys.PLAYER_SIGNATURE, apiKey);
            extra.SetField("auth_source", source);

            if (!string.IsNullOrWhiteSpace(apiKeyExpiresAt))
                extra.SetField("api_key_expires_at", apiKeyExpiresAt);

            return extra;
        }

        private static string ResolveDisplayName(MstJson user, string fallbackId)
        {
            if (TryReadString(user, "display_name", out string displayName) && !string.IsNullOrWhiteSpace(displayName))
                return displayName;

            if (TryReadString(user, "username", out string username) && !string.IsNullOrWhiteSpace(username))
                return username;

            return $"Itch Player {fallbackId}";
        }

        private static string ResolveAvatar(MstJson user)
        {
            if (TryReadString(user, "cover_url", out string coverUrl))
                return coverUrl;

            if (TryReadString(user, "avatar_url", out string avatarUrl))
                return avatarUrl;

            return string.Empty;
        }

        private static bool TryReadString(MstJson json, string fieldName, out string value)
        {
            value = string.Empty;
            MstJson field = json?.GetField(fieldName);

            if (field == null)
                return false;

            value = field.IsNumber ? field.LongValue.ToString() : field.StringValue;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool RequestFailed(UnityWebRequest request)
        {
#if UNITY_2020_1_OR_NEWER
            return request.result != UnityWebRequest.Result.Success;
#else
            return request.isNetworkError || request.isHttpError;
#endif
        }
    }
}
