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

        [Header("YG Settings"), SerializeField, Range(6f, 60f)]
        private float ygSaveInterval = 6f;
        [SerializeField, Range(180f, 600f)]
        private float ygInterstitialAdInterval = 180f;
        [SerializeField]
        private bool ygAutoSendApiReady = true;
        [SerializeField, Range(6f, 60f)]
        private float ygLeaderboardPlayerEntryInterval = 6f;
        [SerializeField, Range(16f, 60f)]
        private float ygLeaderboardEntriesInterval = 16f;

        [Header("Editor Only Settings"), SerializeField]
        private bool useFakeData = true;

        #endregion

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")]
        private static extern string MstGetPlatformId();
#endif

        private GameServiceId serviceId;
        private IGameService _service;
        public static IGameService Service => Instance._service;

        protected override void Awake()
        {
            base.Awake();

            AutodetectPlatformId();

            logger.Info($"Starting {serviceId} game service");

            var options = MstJson.EmptyObject;
#if UNITY_EDITOR
            options.SetField(GameServiceOptionKeys.EDITOR_USE_FAKE_DATA, useFakeData);
#endif

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
                    options.SetField(GameServiceOptionKeys.YG_LEADERBOARD_PLAYER_ENTRY_INTERVAL, ygLeaderboardPlayerEntryInterval);
                    options.SetField(GameServiceOptionKeys.YG_LEADERBOARD_ENTRIES_INTERVAL, ygLeaderboardEntriesInterval);
                    break;
                default:
                    _service = gameObject.AddComponent<SelfService>();
                    break;
            }

            _service.Logger = logger;
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