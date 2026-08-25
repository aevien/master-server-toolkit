using MasterServerToolkit.Json;
using System;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public enum ItchAuthMode
    {
        Auto,
        AppToken,
        OAuth,
        Disabled
    }

    [Serializable]
    public class ItchSdkSettings
    {
        [Tooltip("itch.io application identifier exposed through GameBridge.Service.AppId. Leave empty when the game does not require an application-specific value.")]
        public string appId = "";
        [Tooltip("Fallback ISO language code used when itch.io does not provide a locale, for example en or ru.")]
        public string defaultLang = "en";
        [Tooltip("Authentication source used by the desktop itch.io service. Auto prefers a valid app token and leaves future OAuth available when implemented; Disabled forces guest fallback.")]
        public ItchAuthMode authMode = ItchAuthMode.Auto;
        [Tooltip("HTTPS endpoint used to resolve the current itch.io profile from an app token.")]
        public string profileUrl = ItchService.DefaultProfileUrl;
        [Tooltip("Process environment-variable name containing the itch.io API key supplied by the itch app. Changing the name requires matching launch configuration.")]
        public string apiKeyEnvironmentVariable = ItchService.DefaultApiKeyEnvironmentVariable;
        [Tooltip("Process environment-variable name containing the optional itch.io API-key expiry timestamp.")]
        public string apiKeyExpiresAtEnvironmentVariable = ItchService.DefaultApiKeyExpiresAtEnvironmentVariable;
        [Range(2, 30)]
        [Tooltip("Maximum profile-request duration in realtime seconds. Valid Inspector range: 2 to 30 seconds.")]
        public int profileRequestTimeout = 8;

        [Header("Future Web OAuth")]
        [Tooltip("Reserved itch.io OAuth client identifier for the future Web service. It is not used by the current desktop app-token flow.")]
        public string oauthClientId = "";
        [Tooltip("Reserved OAuth callback URI for the future itch.io Web service. It is not used by the current desktop app-token flow.")]
        public string oauthRedirectUri = "";
        [Tooltip("Reserved space-separated itch.io OAuth scope list. It is not used by the current desktop app-token flow.")]
        public string oauthScope = "profile:me";

        [Header("Editor Testing")]
        [Tooltip("Allows itch.io platform detection in the Unity Editor. Enable only while testing with Editor Api Key or matching environment credentials.")]
        public bool detectInEditor = false;
        [Tooltip("itch.io API key used only for Editor testing when Detect In Editor is enabled. Leave empty to use the configured environment variable.")]
        public string editorApiKey = "";

        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.SetField(nameof(appId), appId);
            json.SetField(nameof(defaultLang), defaultLang);
            json.SetField(nameof(authMode), authMode.ToString());
            json.SetField(nameof(profileUrl), profileUrl);
            json.SetField(nameof(apiKeyEnvironmentVariable), apiKeyEnvironmentVariable);
            json.SetField(nameof(apiKeyExpiresAtEnvironmentVariable), apiKeyExpiresAtEnvironmentVariable);
            json.SetField(nameof(profileRequestTimeout), profileRequestTimeout);
            json.SetField(nameof(oauthClientId), oauthClientId);
            json.SetField(nameof(oauthRedirectUri), oauthRedirectUri);
            json.SetField(nameof(oauthScope), oauthScope);
            json.SetField(nameof(detectInEditor), detectInEditor);
            json.SetField(nameof(editorApiKey), editorApiKey);
            return json;
        }
    }

    public class ItchService : BaseService
    {
        public const string DefaultProfileUrl = "https://api.itch.io/profile";
        public const string DefaultApiKeyEnvironmentVariable = "ITCHIO_API_KEY";
        public const string DefaultApiKeyExpiresAtEnvironmentVariable = "ITCHIO_API_KEY_EXPIRES_AT";

        public override long ServerTime => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public override ServiceDeviceType Device =>
            Application.isMobilePlatform ? ServiceDeviceType.Mobile : ServiceDeviceType.Desktop;

        public override void OnBeforeInit(MstJson options)
        {
            Id = GameServiceId.Itch;
            AppId = ResolveAppId(options);
            Lang = ResolveLang(options);
            Payload = CreatePayload(options);

            Player = AddModule<ItchPlayerModule>();
            IAP = AddModule<ItchInAppPurchaseModule>();
            Leaderboards = AddModule<ItchLeaderboardsModule>();
            Ad = AddModule<ItchAdvertisementModule>();
            Analytics = AddModule<ItchAnalyticsModule>();
            Storage = AddModule<ItchStorageModule>();
            Share = AddModule<ItchShareModule>();

            base.OnBeforeInit(options);
        }

        public static bool HasLaunchCredentials(ItchSdkSettings settings)
        {
            settings ??= new ItchSdkSettings();

#if UNITY_EDITOR
            if (!settings.detectInEditor)
                return false;

            if (!string.IsNullOrWhiteSpace(settings.editorApiKey))
                return true;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            return !string.IsNullOrWhiteSpace(GetEnvironmentValue(settings.apiKeyEnvironmentVariable, DefaultApiKeyEnvironmentVariable));
#endif
        }

        internal static string GetStringOption(MstJson options, string fieldName, string defaultValue)
        {
            MstJson value = options?.GetField(fieldName);
            return value == null || string.IsNullOrWhiteSpace(value.StringValue) ? defaultValue : value.StringValue;
        }

        internal static int GetIntOption(MstJson options, string fieldName, int defaultValue)
        {
            MstJson value = options?.GetField(fieldName);
            return value == null ? defaultValue : value.IntValue;
        }

        internal static ItchAuthMode GetAuthMode(MstJson options)
        {
            string rawValue = GetStringOption(options, nameof(ItchSdkSettings.authMode), ItchAuthMode.Auto.ToString());
            return Enum.TryParse(rawValue, true, out ItchAuthMode mode) ? mode : ItchAuthMode.Auto;
        }

        internal static string GetEnvironmentValue(string configuredName, string defaultName)
        {
            string envName = string.IsNullOrWhiteSpace(configuredName) ? defaultName : configuredName;
            return Environment.GetEnvironmentVariable(envName) ?? string.Empty;
        }

        private static string ResolveAppId(MstJson options)
        {
            string configuredAppId = GetStringOption(options, nameof(ItchSdkSettings.appId), string.Empty);
            return string.IsNullOrWhiteSpace(configuredAppId) ? Application.identifier : configuredAppId;
        }

        private static string ResolveLang(MstJson options)
        {
            string lang = GetStringOption(options, nameof(ItchSdkSettings.defaultLang), "en");
            return string.IsNullOrWhiteSpace(lang) ? "en" : lang;
        }

        private static MstJson CreatePayload(MstJson options)
        {
            var payload = MstJson.CreateObject();
            payload.SetField("auth_mode", GetAuthMode(options).ToString());
            payload.SetField("profile_url", GetStringOption(options, nameof(ItchSdkSettings.profileUrl), DefaultProfileUrl));
            return payload;
        }
    }
}
