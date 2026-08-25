using MasterServerToolkit.Json;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    [Serializable]
    public class VkGamesSdkSettings
    {
        [Tooltip("Exact VK Bridge browser bundle URL loaded when the hosting page has not already provided window.vkBridge. Pin this to a reviewed version; an empty value requires the host page to load VK Bridge itself.")]
        public string bridgeScriptUrl = "https://unpkg.com/@vkontakte/vk-bridge@3.0.2/dist/browser.min.js";

        [Tooltip("VK Storage key containing the game's complete JSON save document. Keep this stable after release so existing player data remains readable.")]
        public string storageKey = "mst_game";

        [Range(6f, 60f)]
        [Tooltip("Realtime seconds between writes to VK Storage. Valid Inspector range: 6 to 60 seconds; repeated writes inside the interval are coalesced to the latest document.")]
        public float saveInterval = 6f;

        [Range(180f, 600f)]
        [Tooltip("Minimum realtime seconds between interstitial-ad requests. Valid Inspector range: 180 to 600 seconds.")]
        public float interstitialAdInterval = 180f;

        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.SetField(nameof(bridgeScriptUrl), bridgeScriptUrl ?? string.Empty);
            json.SetField(nameof(storageKey), storageKey ?? string.Empty);
            json.SetField(nameof(saveInterval), saveInterval);
            json.SetField(nameof(interstitialAdInterval), interstitialAdInterval);
            return json;
        }
    }

    public class VkGamesService : BaseService
    {
        [DllImport("__Internal")] private static extern void Gb_Vk_InitSdk(string bridgeScriptUrl);
        [DllImport("__Internal")] private static extern int Gb_Vk_IsReady();
        [DllImport("__Internal")] private static extern string Gb_Vk_Environment();
        [DllImport("__Internal")] private static extern string Gb_Vk_Device();
        [DllImport("__Internal")] private static extern double Gb_Vk_ServerTime();
        [DllImport("__Internal")] private static extern void Gb_Vk_Dispose();

        private MstJson environment = MstJson.CreateNull();
        private Coroutine environmentRoutine;

        public override bool IsReady => !environment.IsNull && Gb_Vk_IsReady() == 1 && base.IsReady;
        public override long ServerTime => (long)Gb_Vk_ServerTime();

        public override ServiceDeviceType Device =>
            Enum.TryParse(Gb_Vk_Device(), true, out ServiceDeviceType device) ? device : base.Device;

        public override void OnBeforeInit(MstJson options)
        {
            Id = GameServiceId.VKGames;
            environment = MstJson.CreateNull();
            AppId = null;
            Lang = null;
            Payload = MstJson.CreateObject();
            Referrer = new ReferrerInfo();

            Player = AddModule<VkGamesPlayerModule>();
            IAP = AddModule<VkGamesInAppPurchaseModule>();
            Leaderboards = AddModule<VkGamesLeaderboardsModule>();
            Ad = AddModule<VkGamesAdvertisementModule>();
            Analytics = AddModule<VkGamesAnalyticsModule>();
            Storage = AddModule<VkGamesStorageModule>();
            Share = AddModule<VkGamesShareModule>();

            base.OnBeforeInit(options);
            string url = options.HasField(nameof(VkGamesSdkSettings.bridgeScriptUrl))
                ? options[nameof(VkGamesSdkSettings.bridgeScriptUrl)].StringValue
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(url) &&
                (!Uri.TryCreate(url, UriKind.Absolute, out Uri bridgeUri) || bridgeUri.Scheme != Uri.UriSchemeHttps))
            {
                Logger.Error("VK Bridge script URL must be an absolute HTTPS URL or empty when the hosting page preloads the bridge.");
                url = string.Empty;
            }

            Gb_Vk_InitSdk(url);
        }

        public override void OnInit()
        {
            environmentRoutine ??= StartCoroutine(LoadEnvironmentCoroutine());
            base.OnInit();
        }

        private IEnumerator LoadEnvironmentCoroutine()
        {
            yield return null;
            float elapsed = 0f;

            while (Gb_Vk_IsReady() != 1 && elapsed < WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            if (Gb_Vk_IsReady() == 1)
            {
                string json = Gb_Vk_Environment();
                if (MstJson.IsJson(json))
                {
                    var parsedEnvironment = new MstJson(json);
                    if (!parsedEnvironment.IsObject)
                    {
                        Logger.Warn("VK Bridge returned a non-object environment value.");
                        environmentRoutine = null;
                        yield break;
                    }

                    environment = parsedEnvironment;
                    AppId = ReadNestedString(environment, "app", "id");
                    Lang = ReadNestedString(environment, "i18n", "lang");
                    Payload = environment.HasField("payload") ? environment["payload"] : MstJson.CreateObject();
                    Referrer = environment.HasField("referrer") ? ReferrerInfo.FromJson(environment["referrer"]) : new ReferrerInfo();

                    if (environment.HasField("launchParameters") && environment["launchParameters"].IsObject)
                        Options.SetField("launchParameters", environment["launchParameters"]);
                }
                else
                {
                    Logger.Warn("VK Bridge returned invalid environment JSON.");
                }
            }
            else
            {
                Logger.Warn("VK Bridge did not become ready before the service timeout.");
            }

            environmentRoutine = null;
        }

        private static string ReadNestedString(MstJson json, string objectName, string fieldName)
        {
            if (!json.HasField(objectName) || !json[objectName].IsObject || !json[objectName].HasField(fieldName))
                return string.Empty;

            return json[objectName][fieldName].StringValue ?? string.Empty;
        }

        protected void Vk_OnViewVisibility(int hidden) => NotifyOnPause(hidden != 0);

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Gb_Vk_Dispose();
#endif
        }
    }
}
