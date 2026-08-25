using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

#if VKPLAY_STEAMWORKS
using Steamworks;
#endif

namespace MasterServerToolkit.GameService
{
    [Serializable]
    public class VkPlaySdkSettings
    {
        [Tooltip("HTTPS URL template for the VK Play browser JS API. Use {0} where the VK Play GMRID must be inserted. Leave empty only when the hosting page already provides window.iframeApi.")]
        public string webScriptUrlTemplate = "https://vkplay.ru/app/{0}/static/mailru.core.js";
        [Tooltip("VK Play application identifier exposed through GameBridge.Service.AppId.")]
        public string appId = "";
        [Tooltip("Fallback ISO language code used when the launcher does not provide a locale, for example ru or en.")]
        public string defaultLang = "ru";
        [Tooltip("Command-line argument name containing the VK Play user identifier.")]
        public string userIdArgName = VkPlayLaunchArguments.UserIdArgName;
        [Tooltip("Command-line argument name containing the VK Play authentication token.")]
        public string authTokenArgName = VkPlayLaunchArguments.AuthTokenArgName;
        [Tooltip("Command-line argument name containing the launcher locale.")]
        public string localeArgName = VkPlayLaunchArguments.LocaleArgName;
        [Tooltip("Command-line argument name containing the launcher currency code.")]
        public string currencyArgName = VkPlayLaunchArguments.CurrencyArgName;
        [Tooltip("Optional command-line argument name containing a public display name. Leave empty when the launcher does not supply it.")]
        public string userDisplayNameArgName = "";
        [Tooltip("Optional command-line argument name containing a public avatar URL. Leave empty when the launcher does not supply it.")]
        public string userAvatarUrlArgName = "";
        [Tooltip("Uses the initialized Steamworks profile as the preferred source for display name and avatar when VK Play ships through Steam-compatible APIs.")]
        public bool useSteamProfile = true;
        [Tooltip("Allows VK Play platform detection in the Unity Editor by using the Editor test identity below.")]
        public bool detectInEditor = false;
        [Tooltip("Stable VK Play user identifier supplied during Editor testing when Detect In Editor is enabled.")]
        public string editorUserId = "vkplay_editor_user";
        [Tooltip("Public VK Play display name supplied during Editor testing when Detect In Editor is enabled.")]
        public string editorUserDisplayName = "VK Play Player";
        [Tooltip("Optional avatar URL supplied during VK Play Editor testing. Leave empty to test the no-avatar fallback.")]
        public string editorUserAvatarUrl = "";

        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.SetField(nameof(webScriptUrlTemplate), webScriptUrlTemplate);
            json.SetField(nameof(appId), appId);
            json.SetField(nameof(defaultLang), defaultLang);
            json.SetField(nameof(userIdArgName), userIdArgName);
            json.SetField(nameof(authTokenArgName), authTokenArgName);
            json.SetField(nameof(localeArgName), localeArgName);
            json.SetField(nameof(currencyArgName), currencyArgName);
            json.SetField(nameof(userDisplayNameArgName), userDisplayNameArgName);
            json.SetField(nameof(userAvatarUrlArgName), userAvatarUrlArgName);
            json.SetField(nameof(useSteamProfile), useSteamProfile);
            json.SetField(nameof(detectInEditor), detectInEditor);
            json.SetField(nameof(editorUserId), editorUserId);
            json.SetField(nameof(editorUserDisplayName), editorUserDisplayName);
            json.SetField(nameof(editorUserAvatarUrl), editorUserAvatarUrl);
            return json;
        }
    }

    public class VkPlayService : BaseService
    {
        [DllImport("__Internal")] private static extern void Gb_VkPlay_InitSdk(string appId, string scriptUrlTemplate);
        [DllImport("__Internal")] private static extern int Gb_VkPlay_IsReady();
        [DllImport("__Internal")] private static extern string Gb_VkPlay_Environment();
        [DllImport("__Internal")] private static extern string Gb_VkPlay_Device();
        [DllImport("__Internal")] private static extern void Gb_VkPlay_Dispose();

        private bool steamApiInitialized;
        private MstJson webEnvironment = MstJson.CreateNull();
        private Coroutine webEnvironmentRoutine;

        internal bool IsSteamApiInitialized => steamApiInitialized;
        internal bool IsWebRuntime
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public override bool IsReady =>
            (!IsWebRuntime || (!webEnvironment.IsNull && Gb_VkPlay_IsReady() == 1)) &&
            base.IsReady;

        public override long ServerTime => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public override ServiceDeviceType Device
        {
            get
            {
                if (IsWebRuntime && Enum.TryParse(Gb_VkPlay_Device(), true, out ServiceDeviceType device))
                    return device;

                return base.Device;
            }
        }

        public override void OnBeforeInit(MstJson options)
        {
            Id = GameServiceId.VKPlay;
            AppId = IsWebRuntime
                ? options.GetField(nameof(VkPlaySdkSettings.appId)).StringValue
                : ResolveAppId(options);
            Lang = ResolveLang(options);
            Payload = CreatePayload(options);
            Referrer = new ReferrerInfo();
            webEnvironment = MstJson.CreateNull();

            Player = AddModule<VkPlayPlayerModule>();
            IAP = AddModule<VkPlayInAppPurchaseModule>();
            Leaderboards = AddModule<VkPlayLeaderboardsModule>();
            Ad = AddModule<VkPlayAdvertisementModule>();
            Analytics = AddModule<VkPlayAnalyticsModule>();
            Storage = AddModule<VkPlayStorageModule>();
            Share = AddModule<VkPlayShareModule>();

            base.OnBeforeInit(options);

            if (IsWebRuntime)
                InitializeWebSdk(options);
        }

        public override void OnInit()
        {
            if (IsWebRuntime)
                webEnvironmentRoutine ??= StartCoroutine(LoadWebEnvironmentCoroutine());
            else
                TryInitializeSteamApi();

            base.OnInit();
        }

        private void InitializeWebSdk(MstJson options)
        {
            string scriptUrlTemplate = options
                .GetField(nameof(VkPlaySdkSettings.webScriptUrlTemplate))
                .StringValue;

            if (!string.IsNullOrWhiteSpace(scriptUrlTemplate) &&
                !scriptUrlTemplate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Error("VK Play browser JS API URL template must use HTTPS or be empty when the hosting page preloads iframeApi.");
                scriptUrlTemplate = string.Empty;
            }

            Gb_VkPlay_InitSdk(AppId ?? string.Empty, scriptUrlTemplate ?? string.Empty);
        }

        private IEnumerator LoadWebEnvironmentCoroutine()
        {
            yield return null;
            float elapsed = 0f;

            while (Gb_VkPlay_IsReady() != 1 && elapsed < WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            if (Gb_VkPlay_IsReady() == 1)
            {
                string json = Gb_VkPlay_Environment();

                if (MstJson.IsJson(json))
                {
                    var parsed = new MstJson(json);

                    if (parsed.IsObject)
                    {
                        webEnvironment = parsed;
                        ApplyWebEnvironment(parsed);
                    }
                    else
                    {
                        Logger.Warn("VK Play browser JS API returned a non-object environment value.");
                    }
                }
                else
                {
                    Logger.Warn("VK Play browser JS API returned invalid environment JSON.");
                }
            }
            else
            {
                Logger.Warn("VK Play browser JS API did not become ready before the service timeout.");
            }

            webEnvironmentRoutine = null;
        }

        private void ApplyWebEnvironment(MstJson environment)
        {
            if (environment.HasField("app") && environment["app"].IsObject)
            {
                string environmentAppId = environment["app"].GetField("id").StringValue;

                if (!string.IsNullOrWhiteSpace(environmentAppId))
                    AppId = environmentAppId;
            }

            if (environment.HasField("i18n") && environment["i18n"].IsObject)
            {
                string environmentLang = environment["i18n"].GetField("lang").StringValue;

                if (!string.IsNullOrWhiteSpace(environmentLang))
                    Lang = environmentLang;
            }

            if (environment.HasField("currency"))
                Payload.SetField("currency", environment["currency"].StringValue);
        }

        private void Update()
        {
#if VKPLAY_STEAMWORKS
            if (!steamApiInitialized)
                return;

            try
            {
                SteamAPI.RunCallbacks();
            }
            catch (Exception e)
            {
                steamApiInitialized = false;
                Logger.Warn($"VK Play Steam API callbacks stopped: {GetExceptionMessage(e)}");
            }
#endif
        }

        private void OnDestroy()
        {
            if (IsWebRuntime)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Gb_VkPlay_Dispose();
#endif
                return;
            }

#if VKPLAY_STEAMWORKS
            if (!steamApiInitialized)
                return;

            try
            {
                SteamAPI.Shutdown();
            }
            catch (Exception e)
            {
                Logger.Warn($"VK Play Steam API shutdown failed: {GetExceptionMessage(e)}");
            }
            finally
            {
                steamApiInitialized = false;
            }
#endif
        }

        public static bool HasLaunchCredentials(VkPlaySdkSettings settings)
        {
            settings ??= new VkPlaySdkSettings();

#if UNITY_EDITOR
            if (settings.detectInEditor)
                return true;
#endif

            return !string.IsNullOrWhiteSpace(VkPlayLaunchArguments.GetValue(settings.userIdArgName)) &&
                   !string.IsNullOrWhiteSpace(VkPlayLaunchArguments.GetValue(settings.authTokenArgName));
        }

        private void TryInitializeSteamApi()
        {
            if (!Options.GetField(nameof(VkPlaySdkSettings.useSteamProfile)).BoolValue)
                return;

#if VKPLAY_STEAMWORKS
            try
            {
                steamApiInitialized = SteamAPI.Init();

                if (steamApiInitialized)
                    Logger.Info("VK Play Steam API emulation initialized.");
                else
                    Logger.Warn("VK Play Steam API emulation was not initialized. Check VK Play native steam_api DLL setup.");
            }
            catch (Exception e)
            {
                steamApiInitialized = false;
                Logger.Warn($"VK Play Steam API emulation initialization failed: {GetExceptionMessage(e)}");
            }
#else
            Logger.Warn("VK Play Steam profile lookup is disabled. Add VKPLAY_STEAMWORKS define and Steamworks.NET package to enable it.");
#endif
        }

        private static string GetExceptionMessage(Exception e)
        {
            return e.InnerException?.Message ?? e.Message;
        }

        private static string ResolveAppId(MstJson options)
        {
            string configuredAppId = options.GetField(nameof(VkPlaySdkSettings.appId)).StringValue;
            return string.IsNullOrWhiteSpace(configuredAppId) ? Application.identifier : configuredAppId;
        }

        private static string ResolveLang(MstJson options)
        {
            string localeArgName = options.GetField(nameof(VkPlaySdkSettings.localeArgName)).StringValue;
            string lang = VkPlayLaunchArguments.GetValue(localeArgName);

            if (!string.IsNullOrWhiteSpace(lang))
                return lang;

            lang = options.GetField(nameof(VkPlaySdkSettings.defaultLang)).StringValue;
            return string.IsNullOrWhiteSpace(lang) ? "ru" : lang;
        }

        private static MstJson CreatePayload(MstJson options)
        {
            string currency = VkPlayLaunchArguments.GetValue(options.GetField(nameof(VkPlaySdkSettings.currencyArgName)).StringValue);
            var payload = MstJson.CreateObject();

            if (!string.IsNullOrWhiteSpace(currency))
                payload.SetField("currency", currency);

            return payload;
        }
    }

    public static class VkPlayLaunchArguments
    {
        public const string UserIdArgName = "--sz_pers_id";
        public const string AuthTokenArgName = "--sz_token";
        public const string LocaleArgName = "-my_ui_locale_arg";
        public const string CurrencyArgName = "-my_ui_currency_arg";

        public static string GetValue(string argName, string defaultValue = "")
        {
            if (string.IsNullOrWhiteSpace(argName))
                return defaultValue;

            if (Mst.Args.IsProvided(argName))
                return Mst.Args.AsString(argName, defaultValue);

            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (string.Equals(arg, argName, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1 < args.Length ? args[i + 1] : defaultValue;
                }

                string prefix = argName.EndsWith("=") ? argName : $"{argName}=";

                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return arg.Substring(prefix.Length);
            }

            string envName = argName.TrimStart('-').Replace("-", "_").ToUpperInvariant();
            string envValue = Environment.GetEnvironmentVariable(envName);
            return string.IsNullOrWhiteSpace(envValue) ? defaultValue : envValue;
        }
    }
}
