using MasterServerToolkit.Json;
using MasterServerToolkit.Utils;
using System;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.GameService
{
    public class GameBridge : SingletonBehaviour<GameBridge>
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private GameServiceId serviceId;
        [SerializeField]
        private bool useSelectedServiceId;

        [Header("YG Settings"), SerializeField, Range(5f, 60f)]
        private float ygSaveInterval = 5f;
        [SerializeField, Range(180f, 600f)]
        private float ygInterstitialAdInterval = 180f;
        [SerializeField]
        private bool ygAutoSendApiReady = true;

        #endregion

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")]
        private static extern string MstGetPlatformId();
#endif

        private IGameService _service;
        public static IGameService Service => Instance._service;

        protected override void Awake()
        {
            base.Awake();

            if (!useSelectedServiceId)
            {
                AutodetectPlatformId();
            }

            var options = MstJson.EmptyObject;

            switch (serviceId)
            {
                case GameServiceId.PlayWeb3:
                    _service = gameObject.AddComponent<PlayWeb3Service>();
                    break;
                case GameServiceId.YandexGames:
                    _service = gameObject.AddComponent<YandexGamesService>();
                    options.SetField(GameServiceOptionKeys.YG_SAVE_DATA_INTERVAL, ygSaveInterval);
                    options.SetField(GameServiceOptionKeys.YG_INTERSTITIAL_AD_INTERVAL, ygInterstitialAdInterval);
                    options.SetField(GameServiceOptionKeys.YG_AUTOSEND_API_READY, ygAutoSendApiReady);
                    break;
                default:
                    _service = gameObject.AddComponent<SelfService>();
                    break;
            }

            _service.Init(options);
        }

        private void AutodetectPlatformId()
        {
            string platformId = "Self";
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            platformId = MstGetPlatformId();
#endif
            serviceId = Enum.Parse<GameServiceId>(platformId);
        }

        private void Start()
        {
            name = "MST_GAME_BRIDGE";
        }
    }
}