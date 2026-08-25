using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

#if VKPLAY_STEAMWORKS
using Steamworks;
#endif

namespace MasterServerToolkit.GameService
{
    public class VkPlayPlayerModule : BasePlayerModule
    {
        [DllImport("__Internal")] private static extern int Gb_VkPlay_IsReady();
        [DllImport("__Internal")] private static extern void Gb_VkPlay_GetPlayer();
        [DllImport("__Internal")] private static extern void Gb_VkPlay_Authenticate();
        [DllImport("__Internal")] private static extern void Gb_VkPlay_GetFriends(int social);

        private string authToken = string.Empty;
        private Coroutine webInitRoutine;
        private bool authenticationPending;
        private bool notifyAuthenticationOnPlayer;
        private Action<MstJson> friendsCallback;
        private Action<MstJson> socialFriendsCallback;

        public bool IsFriendsSupported =>
            Service is VkPlayService vkPlayService && vkPlayService.IsWebRuntime;

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = true;

            if (service is VkPlayService vkPlayService && vkPlayService.IsWebRuntime)
            {
                IsAuthenticationSupported = true;
                IsGuest = true;
                Extra = MstJson.CreateObject();
                webInitRoutine ??= StartCoroutine(InitializeWebPlayerCoroutine());
                return;
            }

            IsAuthenticationSupported = false;
            ResolvePlayerInfo();
        }

        private IEnumerator InitializeWebPlayerCoroutine()
        {
            yield return null;
            float elapsed = 0f;

            while (Gb_VkPlay_IsReady() != 1 && elapsed < Service.WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            if (Gb_VkPlay_IsReady() == 1)
            {
                Gb_VkPlay_GetPlayer();
            }
            else
            {
                IsGuest = true;
                IsReady = true;
                Logger.Warn("VK Play browser player API did not become ready before the service timeout.");
                NotifyOnInfoChanged();
            }

            webInitRoutine = null;
        }

        public override void Authenticate(SuccessCallback callback)
        {
            if (Service is VkPlayService vkPlayService && vkPlayService.IsWebRuntime)
            {
                if (authenticationPending)
                {
                    callback?.Invoke(false, "authentication_in_progress");
                    return;
                }

                base.Authenticate(callback);

                if (!IsGuest && !string.IsNullOrWhiteSpace(Id))
                {
                    NotifyOnAuthenticated(true, string.Empty);
                    return;
                }

                authenticationPending = true;
                Gb_VkPlay_Authenticate();
                return;
            }

            base.Authenticate(callback);
            NotifyOnAuthenticated(false, "VK Play does not support interactive platform authentication");
        }

        protected void VkPlay_OnAuthentication(string json)
        {
            if (!MstJson.IsJson(json))
            {
                authenticationPending = false;
                notifyAuthenticationOnPlayer = false;
                NotifyOnAuthenticated(false, "invalid_vkplay_auth_response");
                return;
            }

            var result = new MstJson(json);
            bool success = result.HasField("success") && result["success"].BoolValue;

            if (!success)
            {
                authenticationPending = false;
                notifyAuthenticationOnPlayer = false;
                string error = result.HasField("error")
                    ? result["error"].StringValue
                    : "vkplay_authentication_failed";
                NotifyOnAuthenticated(false, error);
                return;
            }

            notifyAuthenticationOnPlayer = true;
        }

        protected void VkPlay_OnLoginStatus(string json)
        {
            if (!MstJson.IsJson(json))
                Logger.Warn("VK Play browser login status returned invalid JSON.");
        }

        public void GetFriends(bool includeSocialNetworkFriends, Action<MstJson> callback)
        {
            if (!IsFriendsSupported || Gb_VkPlay_IsReady() != 1)
            {
                callback?.Invoke(CreateErrorResult("vkplay_friends_unavailable"));
                return;
            }

            Action<MstJson> pendingCallback = includeSocialNetworkFriends
                ? socialFriendsCallback
                : friendsCallback;

            if (pendingCallback != null)
            {
                callback?.Invoke(CreateErrorResult("vkplay_friends_request_in_progress"));
                return;
            }

            if (includeSocialNetworkFriends)
                socialFriendsCallback = callback;
            else
                friendsCallback = callback;

            Gb_VkPlay_GetFriends(includeSocialNetworkFriends ? 1 : 0);
        }

        protected void VkPlay_OnFriends(string json)
        {
            CompleteFriendsRequest(ref friendsCallback, json);
        }

        protected void VkPlay_OnSocialFriends(string json)
        {
            CompleteFriendsRequest(ref socialFriendsCallback, json);
        }

        protected void VkPlay_OnPlayer(string json)
        {
            if (!MstJson.IsJson(json))
            {
                CompleteWebPlayerUpdate(false, "invalid_vkplay_player_response");
                return;
            }

            var data = new MstJson(json);
            Id = data.HasField("id") ? data["id"].StringValue : string.Empty;
            Name = data.HasField("name") ? data["name"].StringValue : string.Empty;
            Avatar = data.HasField("avatar") ? data["avatar"].StringValue : string.Empty;
            IsGuest = !data.HasField("is_guest") || data["is_guest"].BoolValue;
            Extra = data.HasField("extra") && data["extra"].IsObject
                ? data["extra"]
                : MstJson.CreateObject();

            if (string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Id))
                Name = $"VK Play Player {Id}";

            string error = data.HasField("error") ? data["error"].StringValue : string.Empty;
            bool identityConfirmed = !IsGuest && !string.IsNullOrWhiteSpace(Id) &&
                Extra.HasField(GameServiceKeys.PLAYER_SIGNATURE) &&
                !string.IsNullOrWhiteSpace(Extra[GameServiceKeys.PLAYER_SIGNATURE].StringValue);

            CompleteWebPlayerUpdate(identityConfirmed, error);
        }

        private void CompleteWebPlayerUpdate(bool identityConfirmed, string error)
        {
            IsReady = true;
            NotifyOnInfoChanged();

            if (!notifyAuthenticationOnPlayer)
                return;

            authenticationPending = false;
            notifyAuthenticationOnPlayer = false;
            NotifyOnAuthenticated(
                identityConfirmed,
                identityConfirmed ? string.Empty :
                    (string.IsNullOrWhiteSpace(error) ? "vkplay_identity_unavailable" : error));
        }

        private static void CompleteFriendsRequest(ref Action<MstJson> callback, string json)
        {
            Action<MstJson> completedCallback = callback;
            callback = null;

            if (completedCallback == null)
                return;

            completedCallback.Invoke(MstJson.IsJson(json)
                ? new MstJson(json)
                : CreateErrorResult("invalid_vkplay_friends_response"));
        }

        private static MstJson CreateErrorResult(string error)
        {
            MstJson result = MstJson.CreateObject();
            result.SetField("status", "error");
            result.SetField("error", error);
            return result;
        }

        private void ResolvePlayerInfo()
        {
            string userId = VkPlayLaunchArguments.GetValue(Service.Options.GetField(nameof(VkPlaySdkSettings.userIdArgName)).StringValue);
            authToken = VkPlayLaunchArguments.GetValue(Service.Options.GetField(nameof(VkPlaySdkSettings.authTokenArgName)).StringValue);

#if UNITY_EDITOR
            if (Service.Options.GetField(nameof(VkPlaySdkSettings.detectInEditor)).BoolValue)
            {
                if (string.IsNullOrWhiteSpace(userId))
                    userId = Service.Options.GetField(nameof(VkPlaySdkSettings.editorUserId)).StringValue;

                if (string.IsNullOrWhiteSpace(authToken))
                    authToken = "editor-token";
            }
#endif

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(authToken))
            {
                IsGuest = true;
                Extra = MstJson.CreateObject();
                Logger.Error("VK Play player credentials are missing. Service will not become ready.");
                return;
            }

            VkPlayPlayerProfile profile = ResolveProfile(userId, Service.Options);
            Id = userId;
            Name = ResolveDisplayName(userId, profile);
            Avatar = ResolveAvatar(profile);
            IsGuest = false;
            Extra = CreateExtraData(authToken, profile);
            IsReady = true;
            Logger.Info(
                $"VK Play player resolved. UserId={Id}, TokenPresent={!string.IsNullOrWhiteSpace(authToken)}, " +
                $"DisplayName={Name}, " +
                $"NameSource={(string.IsNullOrWhiteSpace(profile.Source) ? "fallback" : profile.Source)}, " +
                $"AvatarPresent={!string.IsNullOrWhiteSpace(Avatar)}");
            NotifyOnInfoChanged();
        }

        private string ResolveDisplayName(string userId, VkPlayPlayerProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.DisplayName))
                return profile.DisplayName;

            return $"VK Play Player {userId}";
        }

        private static string ResolveAvatar(VkPlayPlayerProfile profile)
        {
            return string.IsNullOrWhiteSpace(profile.AvatarUrl) ? string.Empty : profile.AvatarUrl;
        }

        private static MstJson CreateExtraData(string token, VkPlayPlayerProfile profile)
        {
            var extra = MstJson.CreateObject();
            extra.SetField(GameServiceKeys.PLAYER_SIGNATURE, token);
            extra.SetField("token", token);
            extra.SetField("vkplay_token", token);

            if (!string.IsNullOrWhiteSpace(profile.Source))
                extra.SetField("profile_source", profile.Source);

            return extra;
        }

        private VkPlayPlayerProfile ResolveProfile(string userId, MstJson options)
        {
            var profile = ResolveFromLaunchArguments(options);

#if UNITY_EDITOR
            if (options.GetField(nameof(VkPlaySdkSettings.detectInEditor)).BoolValue)
            {
                ApplyEditorProfile(profile, options);
                return profile;
            }
#endif

            if (options.GetField(nameof(VkPlaySdkSettings.useSteamProfile)).BoolValue)
            {
                var steamProfile = ResolveFromSteamworks(userId);

                if (!string.IsNullOrWhiteSpace(steamProfile.DisplayName))
                    profile.DisplayName = steamProfile.DisplayName;

                if (!string.IsNullOrWhiteSpace(steamProfile.AvatarUrl))
                    profile.AvatarUrl = steamProfile.AvatarUrl;

                if (!string.IsNullOrWhiteSpace(steamProfile.Source))
                    profile.Source = steamProfile.Source;
            }

            return profile;
        }

        private static VkPlayPlayerProfile ResolveFromLaunchArguments(MstJson options)
        {
            var profile = new VkPlayPlayerProfile();
            string displayNameArgName = options.GetField(nameof(VkPlaySdkSettings.userDisplayNameArgName)).StringValue;
            string avatarUrlArgName = options.GetField(nameof(VkPlaySdkSettings.userAvatarUrlArgName)).StringValue;

            profile.DisplayName = VkPlayLaunchArguments.GetValue(displayNameArgName);
            profile.AvatarUrl = VkPlayLaunchArguments.GetValue(avatarUrlArgName);

            if (!string.IsNullOrWhiteSpace(profile.DisplayName) || !string.IsNullOrWhiteSpace(profile.AvatarUrl))
                profile.Source = "launch_arguments";

            return profile;
        }

        private static void ApplyEditorProfile(VkPlayPlayerProfile profile, MstJson options)
        {
            string editorName = options.GetField(nameof(VkPlaySdkSettings.editorUserDisplayName)).StringValue;
            string editorAvatar = options.GetField(nameof(VkPlaySdkSettings.editorUserAvatarUrl)).StringValue;

            if (!string.IsNullOrWhiteSpace(editorName))
                profile.DisplayName = editorName;

            if (!string.IsNullOrWhiteSpace(editorAvatar))
                profile.AvatarUrl = editorAvatar;

            if (!string.IsNullOrWhiteSpace(profile.DisplayName) || !string.IsNullOrWhiteSpace(profile.AvatarUrl))
                profile.Source = "editor";
        }

        private VkPlayPlayerProfile ResolveFromSteamworks(string userId)
        {
            var profile = new VkPlayPlayerProfile();

#if VKPLAY_STEAMWORKS
            try
            {
                string personaName = SteamFriends.GetPersonaName();

                if (!string.IsNullOrWhiteSpace(personaName))
                {
                    profile.DisplayName = personaName;
                    profile.Source = "steamworks";
                }

                CSteamID steamId = SteamUser.GetSteamID();

                if (TryGetSteamAvatarImageId(steamId, out int imageId) &&
                    TryCreateAvatarFile(imageId, userId, out string avatarUrl))
                {
                    profile.AvatarUrl = avatarUrl;
                    profile.Source = "steamworks";
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"VK Play Steam profile resolution failed: {e.Message}");
            }
#endif

            return profile;
        }

#if VKPLAY_STEAMWORKS
        private static bool TryGetSteamAvatarImageId(CSteamID steamId, out int imageId)
        {
            imageId = SteamFriends.GetLargeFriendAvatar(steamId);

            if (imageId > 0)
                return true;

            imageId = SteamFriends.GetMediumFriendAvatar(steamId);
            return imageId > 0;
        }

        private static bool TryCreateAvatarFile(int imageId, string userId, out string avatarUrl)
        {
            avatarUrl = string.Empty;

            if (!SteamUtils.GetImageSize(imageId, out uint width, out uint height) || width == 0 || height == 0)
                return false;

            int bufferSize = checked((int)(width * height * 4));
            byte[] rgba = new byte[bufferSize];

            if (!SteamUtils.GetImageRGBA(imageId, rgba, bufferSize))
                return false;

            FlipImageRowsInPlace(rgba, (int)width, (int)height);

            Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false, true);
            texture.LoadRawTextureData(rgba);
            texture.Apply();

            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.Destroy(texture);

            string cacheDirectory = Path.Combine(Application.temporaryCachePath, "vkplay-avatars");
            Directory.CreateDirectory(cacheDirectory);

            string filePath = Path.Combine(cacheDirectory, $"{SanitizeFileName(userId)}.png");
            File.WriteAllBytes(filePath, png);
            avatarUrl = new Uri(filePath).AbsoluteUri;
            return true;
        }

        private static void FlipImageRowsInPlace(byte[] rgba, int width, int height)
        {
            int rowSize = checked(width * 4);
            byte[] row = new byte[rowSize];

            for (int y = 0; y < height / 2; y++)
            {
                int top = y * rowSize;
                int bottom = (height - y - 1) * rowSize;

                Buffer.BlockCopy(rgba, top, row, 0, rowSize);
                Buffer.BlockCopy(rgba, bottom, rgba, top, rowSize);
                Buffer.BlockCopy(row, 0, rgba, bottom, rowSize);
            }
        }
#endif

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "player";

            string sanitized = value;

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                sanitized = sanitized.Replace(invalidChar, '_');

            return sanitized;
        }

        private sealed class VkPlayPlayerProfile
        {
            public string DisplayName { get; set; } = string.Empty;
            public string AvatarUrl { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
        }
    }
}
