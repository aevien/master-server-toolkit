using MasterServerToolkit.Json;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.GameService
{
    [DefaultExecutionOrder(-100)]
    public class GameBridge : SingletonBehaviour<GameBridge>
    {
        [Header("Settings"), SerializeField, Tooltip("Maximum time in realtime seconds that platform modules may wait for their SDK to become ready before reporting initialization failure. Use a positive value.")]
        private float waitForReadyTime = 10f;

        [Header("Services")]
        [Tooltip("Yandex Games SDK timing and API-ready settings used when the WebGL platform detector selects Yandex Games.")]
        public YandexGameSdkSettings yandexGames = new();
        [Tooltip("VK Play Web iframe/JS API settings plus guarded desktop launch-argument and Steam-profile compatibility settings.")]
        public VkPlaySdkSettings vkPlay = new();
        [FormerlySerializedAs("vkSocial")]
        [Tooltip("VK Games Bridge URL, cloud-storage key, and advertising cooldown settings used when the WebGL URL contains a valid VK Games launch contract.")]
        public VkGamesSdkSettings vkGames = new();
        [Tooltip("itch.io desktop authentication and future Web OAuth settings used when itch.io launch credentials are detected.")]
        public ItchSdkSettings itch = new();
        [FormerlySerializedAs("fakeGames")]
        [Tooltip("Local platform identity and purchase catalogue used when the Editor service is selected.")]
        public EditorSdkSettings editor = new();

        private GameServiceId serviceId = GameServiceId.Editor;
        private IService service;
        private MstJson options = MstJson.CreateObject();

        public static IService Service => Instance != null ? Instance.service : null;

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")] 
        private static extern string MstGetPlatformId();
#endif

        protected override void Awake()
        {
            base.Awake();
            if (isNowDestroying) return;

            name = "MST_GAME_BRIDGE";

            serviceId = DetectPlatformIdSafe();
            logger.Info($"Starting {serviceId} service");

            // Select concrete service by detected platform.
            switch (serviceId)
            {
                case GameServiceId.Editor:
                    service = gameObject.AddComponent<EditorService>();
                    options = editor.ToJson();
                    break;

                case GameServiceId.YandexGames:
                    service = gameObject.AddComponent<YandexGamesService>();
                    options = yandexGames.ToJson();
                    break;

                case GameServiceId.VKPlay:
                    service = gameObject.AddComponent<VkPlayService>();
                    options = vkPlay.ToJson();
                    break;

                case GameServiceId.VKGames:
                    service = gameObject.AddComponent<VkGamesService>();
                    options = vkGames.ToJson();
                    break;

                case GameServiceId.Itch:
                    service = gameObject.AddComponent<ItchService>();
                    options = itch.ToJson();
                    break;

                case GameServiceId.Desktop:
                    service = gameObject.AddComponent<DesktopService>();
                    break;

                case GameServiceId.Web:
                    service = gameObject.AddComponent<WebService>();
                    break;

                default:
                    service = gameObject.AddComponent<DesktopService>();
                    break;
            }

            service.Logger = logger;
            service.WaitForReadyTime = waitForReadyTime;
            service.OnBeforeInit(options);
        }

        private void OnValidate()
        {
            yandexGames ??= new YandexGameSdkSettings();
            vkPlay ??= new VkPlaySdkSettings();
            vkGames ??= new VkGamesSdkSettings();
            itch ??= new ItchSdkSettings();
            editor ??= new EditorSdkSettings();

            if (string.IsNullOrWhiteSpace(editor.userId))
                editor.userId = Guid.NewGuid().ToString();

        }

        IEnumerator Start()
        {
            if (isNowDestroying || service == null)
                yield break;

            service.OnInit();
            yield return null;
            service.OnAfterInit();
        }

        private GameServiceId DetectPlatformIdSafe()
        {
            string platformId;
#if UNITY_EDITOR
            itch ??= new ItchSdkSettings();
            vkPlay ??= new VkPlaySdkSettings();
            platformId = VkPlayService.HasLaunchCredentials(vkPlay)
                ? "VKPlay"
                : ItchService.HasLaunchCredentials(itch) ? "Itch" : "Editor";
#elif UNITY_WEBGL && !UNITY_EDITOR
            platformId = MstGetPlatformId();
#else
            itch ??= new ItchSdkSettings();
            vkPlay ??= new VkPlaySdkSettings();
            platformId = VkPlayService.HasLaunchCredentials(vkPlay)
                ? "VKPlay"
                : ItchService.HasLaunchCredentials(itch) ? "Itch" : "Desktop";
#endif
            if (!Enum.TryParse(platformId, true, out GameServiceId id))
            {
                id = GetDefaultFallbackServiceId();
                logger.Warn($"Unknown platformId '{platformId}', using {id}");
            }

#if !UNITY_EDITOR
            if (id == GameServiceId.Editor)
            {
                id = GetDefaultFallbackServiceId();
                logger.Warn($"Editor service is not available outside Unity Editor, using {id}");
            }
#endif

            return id;
        }

        private GameServiceId GetDefaultFallbackServiceId()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return GameServiceId.Web;
#elif UNITY_EDITOR
            return GameServiceId.Editor;
#else
            return GameServiceId.Desktop;
#endif
        }
    }
}
